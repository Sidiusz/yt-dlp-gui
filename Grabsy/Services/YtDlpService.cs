using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Grabsy.Models;

namespace Grabsy.Services;

/// <summary>Options for a single download, resolved from settings + UI overrides.</summary>
public sealed class DownloadOptions
{
    public bool AudioOnly;
    public bool VideoOnly;                   // video stream without any audio track
    public string Quality = "1080";          // best / 2160 / 1440 / 1080 / 720 / 480
    public string Container = "mp4";         // mp4 / mkv / webm
    public string Codec = "auto";            // auto / h264 / h265 / vp9 / av1
    public string AudioFormat = "mp3";       // mp3 / m4a / opus / flac / best
    public string AudioQuality = "192";      // kbps or "best"
    public bool EmbedThumbnail;
    public bool EmbedMetadata;
    public bool EmbedSubtitles;
    public bool WriteSubtitles;
    public List<string> SubtitleLanguages = new();  // explicit selected sub languages
    public List<string> AudioLanguages = new();      // explicit selected audio languages (>1 = mux, forces mkv)
    public string SectionStart = "";         // "MM:SS" / "HH:MM:SS" / empty
    public string SectionEnd = "";
    public int ConcurrentFragments = 4;
    public string FilenameTemplate = "%(title)s.%(ext)s";
    public string OutputFolder = "";
    public bool Overwrite = false;            // true = --force-overwrites; false = unique " (n)" name
    public string? ResolvedOutputPath = null; // absolute target path once resolved

    // mkv is required to hold multiple audio streams.
    public string EffectiveContainer => AudioLanguages.Count > 1 ? "mkv" : Container;

    public string Label
    {
        get
        {
            if (AudioOnly) return $"{AudioFormat.ToUpperInvariant()} audio";
            var q = Quality == "best" ? "Best" : $"{Quality}p";
            var c = Codec == "auto" ? "" : " " + Codec.ToUpperInvariant();
            return $"{q}{c} {EffectiveContainer.ToUpperInvariant()}";
        }
    }
}

public sealed class YtDlpService
{
    private static readonly Lazy<YtDlpService> _instance = new(() => new YtDlpService());
    public static YtDlpService Instance => _instance.Value;

    private readonly BinaryManager _bin = BinaryManager.Instance;

    private static readonly Regex ProgressRx =
        new(@"\[download\]\s+(?<p>\d+(?:\.\d+)?)%", RegexOptions.Compiled);

    /// <summary>Run `yt-dlp -J` and distill metadata + available heights.</summary>
    public async Task<VideoInfo> ProbeAsync(string url, CancellationToken ct = default)
    {
        var args = $"-J --no-warnings --no-playlist -4 --socket-timeout 30 --extractor-retries 3 --ffmpeg-location \"{_bin.BinDir}\" \"{url}\"";
        var (code, stdout, stderr) = await RunCaptureAsync(args, ct);
        if (code != 0 || string.IsNullOrWhiteSpace(stdout))
            throw new InvalidOperationException(FirstError(stderr) ?? "yt-dlp could not read this URL.");

        using var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;

        var heights = new SortedSet<int>();
        var audioLangs = new Dictionary<string, MediaTrack>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("formats", out var formats) && formats.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in formats.EnumerateArray())
            {
                if (f.TryGetProperty("height", out var h) && h.ValueKind == JsonValueKind.Number)
                {
                    var hv = h.GetInt32();
                    if (hv > 0) heights.Add(hv);
                }

                // Audio-only formats carry the per-language tracks (multi-audio sources).
                var acodec = GetStr(f, "acodec");
                var vcodec = GetStr(f, "vcodec");
                bool isAudioOnly = acodec is not null && acodec != "none" && (vcodec is null || vcodec == "none");
                if (isAudioOnly)
                {
                    var lang = GetStr(f, "language");
                    if (!string.IsNullOrWhiteSpace(lang) && !audioLangs.ContainsKey(lang))
                        audioLangs[lang] = new MediaTrack { Code = lang, Display = LangDisplay(lang), Selected = true };
                }
            }
        }

        var subs = new List<MediaTrack>();
        if (root.TryGetProperty("subtitles", out var subsEl) && subsEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in subsEl.EnumerateObject())
                subs.Add(new MediaTrack { Code = prop.Name, Display = LangDisplay(prop.Name) });
        }

        return new VideoInfo
        {
            Url = url,
            Title = GetStr(root, "title") ?? "Untitled",
            Uploader = GetStr(root, "uploader") ?? GetStr(root, "channel"),
            ThumbnailUrl = GetStr(root, "thumbnail"),
            Duration = root.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetDouble() : 0,
            AvailableHeights = heights.Reverse().ToList(),
            AudioTracks = audioLangs.Values.OrderBy(t => t.Code).ToList(),
            Subtitles = subs.OrderBy(t => t.Code).ToList()
        };
    }

    /// <summary>Build the yt-dlp argument string for a download.</summary>
    public string BuildArgs(string url, DownloadOptions o)
    {
        var sb = new StringBuilder();
        sb.Append("--no-playlist --newline --no-color --ignore-config ");
        sb.Append($"--ffmpeg-location \"{_bin.BinDir}\" ");
        if (!string.IsNullOrEmpty(o.ResolvedOutputPath))
            sb.Append($"-o \"{o.ResolvedOutputPath}\" ");
        else
            sb.Append($"-P \"{o.OutputFolder}\" -o \"{o.FilenameTemplate}\" ");
        if (o.Overwrite) sb.Append("--force-overwrites ");
        sb.Append($"--concurrent-fragments {Math.Max(1, o.ConcurrentFragments)} ");
        // Re-fetch fragments whose signed URLs expire mid-download — the usual
        // cause of "HTTP Error 403: Forbidden" on long/large videos.
        sb.Append("--retries 10 --fragment-retries 10 --extractor-retries 3 --file-access-retries 5 ");
        // -4: dodge flaky IPv6 ("cannot resolve youtube.com"). chunked HTTP reads
        // avoid the "Got error ... bytes read" incomplete-read drop on big 4K files.
        sb.Append("-4 --socket-timeout 30 --http-chunk-size 10M ");

        // Time-range trim (re-encodes cut points accurately).
        var section = BuildSection(o.SectionStart, o.SectionEnd);
        if (section != null)
            sb.Append($"--download-sections \"{section}\" --force-keyframes-at-cuts ");

        if (o.AudioOnly)
        {
            sb.Append("-x ");
            if (!string.Equals(o.AudioFormat, "best", StringComparison.OrdinalIgnoreCase))
                sb.Append($"--audio-format {o.AudioFormat} ");
            var aq = string.Equals(o.AudioQuality, "best", StringComparison.OrdinalIgnoreCase) ? "0" : o.AudioQuality + "K";
            sb.Append($"--audio-quality {aq} ");
            if (o.EmbedThumbnail) sb.Append("--embed-thumbnail ");
            if (o.EmbedMetadata) sb.Append("--embed-metadata ");
        }
        else
        {
            string h = o.Quality == "best" ? "" : $"[height<=?{o.Quality}]";

            // Codec preference via format-sort (graceful: falls back if absent).
            var vcodec = CodecSortKey(o.Codec);
            if (vcodec != null) sb.Append($"-S \"vcodec:{vcodec}\" ");

            if (o.VideoOnly)
            {
                // Video stream only — no audio merged.
                sb.Append($"-f \"bv*{h}/b{h}\" ");
            }
            else if (o.AudioLanguages.Count > 1)
            {
                var joined = string.Join("+", o.AudioLanguages.Select(l => $"ba[language={l}]"));
                sb.Append($"-f \"bv*{h}+{joined}/b{h}\" --audio-multistreams ");
            }
            else if (o.AudioLanguages.Count == 1)
            {
                sb.Append($"-f \"bv*{h}+ba[language={o.AudioLanguages[0]}]/bv*{h}+ba/b{h}\" ");
            }
            else
            {
                sb.Append($"-f \"bv*{h}+ba/b{h}\" ");
            }

            sb.Append($"--merge-output-format {o.EffectiveContainer} ");
            if (o.EmbedThumbnail) sb.Append("--embed-thumbnail ");
            if (o.EmbedMetadata) sb.Append("--embed-metadata ");
            if (o.SubtitleLanguages.Count > 0)
            {
                sb.Append($"--sub-langs {string.Join(",", o.SubtitleLanguages)} ");
                // Default to embedding when subs were chosen but no mode flagged.
                if (o.EmbedSubtitles || !o.WriteSubtitles) sb.Append("--embed-subs ");
                if (o.WriteSubtitles) sb.Append("--write-subs ");
            }
        }

        sb.Append($"\"{url}\"");
        return sb.ToString();
    }

    // null = no codec preference (auto). Names map to yt-dlp format-sort tokens.
    private static string? CodecSortKey(string codec) => codec?.ToLowerInvariant() switch
    {
        "h264" => "h264",
        "h265" => "h265",
        "vp9" => "vp9",
        "av1" => "av01",
        _ => null
    };

    // Build a --download-sections spec. Empty start/end allowed on either side.
    private static string? BuildSection(string start, string end)
    {
        start = start?.Trim() ?? "";
        end = end?.Trim() ?? "";
        if (start.Length == 0 && end.Length == 0) return null;
        var s = start.Length == 0 ? "0" : start;
        var e = end.Length == 0 ? "inf" : end;
        return $"*{s}-{e}";
    }

    /// <summary>Run a download, streaming progress to the job. Resolves final path on success.
    /// <paramref name="ui"/> marshals job mutations onto the UI thread — the Process
    /// callbacks fire on the thread pool and WinUI bindings are thread-affine.</summary>
    public async Task DownloadAsync(DownloadJob job, DownloadOptions o, CancellationToken ct,
        Action<Action> ui)
    {
        // Resolve the exact target path up-front so Play/size point at the real
        // file; add a " (n)" suffix unless overwriting is enabled.
        {
            var target = await ResolveTargetPathAsync(job.Url, o, ct);
            if (!string.IsNullOrEmpty(target))
            {
                o.ResolvedOutputPath = o.Overwrite ? target! : Uniquify(target!);
                ui(() => job.OutputPath = o.ResolvedOutputPath);
            }
        }

        var args = BuildArgs(job.Url, o);
        var psi = NewPsi(args);
        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

        string? finalPath = o.ResolvedOutputPath;
        var errBuf = new StringBuilder();
        int lastPct = -1;

        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            var line = e.Data;

            var m = ProgressRx.Match(line);
            if (m.Success && double.TryParse(m.Groups["p"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var pct))
            {
                // Throttle to whole-percent steps so a hidden window doesn't pile
                // up thousands of dispatcher callbacks that flush on reopen.
                int ip = (int)pct;
                if (ip == lastPct) return;
                lastPct = ip;
                ui(() => { job.Indeterminate = false; job.Progress = pct / 100.0; job.Status = $"Downloading {ip}%"; });
            }
            else if (line.Contains("[Merger]") || line.Contains("[ExtractAudio]") || line.Contains("[Fixup"))
            {
                ui(() => { job.Indeterminate = true; job.Status = "Processing…"; });
            }
            else if (line.Contains("[download] Destination:"))
            {
                // Ignore per-stream intermediates ("name.f137.mp4"); the merged
                // output is reported separately on the "Merging formats" line.
                var dest = line.Substring(line.IndexOf(':') + 1).Trim();
                if (!Regex.IsMatch(dest, @"\.f\d+\.[A-Za-z0-9]+$")) finalPath = dest;
            }
            else if (line.Contains("Merging formats into"))
            {
                var i = line.IndexOf('"');
                var j = line.LastIndexOf('"');
                if (i >= 0 && j > i) finalPath = line.Substring(i + 1, j - i - 1);
            }
            else if (line.Contains("has already been downloaded"))
            {
                ui(() => job.Status = "Already downloaded");
            }
        };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) errBuf.AppendLine(e.Data); };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        using (ct.Register(() => { try { if (!proc.HasExited) proc.Kill(true); } catch { } }))
        {
            await proc.WaitForExitAsync(CancellationToken.None);
        }

        // Past the await we are back on the caller's UI thread, so terminal
        // mutations are safe and synchronous — no enqueue race with RunJobAsync.
        if (ct.IsCancellationRequested)
        {
            job.State = JobState.Canceled; job.Status = "Canceled";
            return;
        }

        if (proc.ExitCode == 0)
        {
            job.Progress = 1; job.Indeterminate = false;
            job.OutputPath = PickFinalPath(o.ResolvedOutputPath, finalPath, o.OutputFolder);
            job.State = JobState.Completed; job.Status = "Done";
        }
        else
        {
            job.State = JobState.Failed;
            job.Status = FirstError(errBuf.ToString()) ?? "Download failed";
        }
    }

    // Run yt-dlp in --print mode to learn the target filename, then force the
    // expected final extension (container for video, chosen format for audio).
    private async Task<string?> ResolveTargetPathAsync(string url, DownloadOptions o, CancellationToken ct)
    {
        try
        {
            var sb = new StringBuilder();
            sb.Append("--no-playlist --no-warnings --ignore-config --skip-download -4 --socket-timeout 30 --extractor-retries 3 ");
            sb.Append($"-P \"{o.OutputFolder}\" -o \"{o.FilenameTemplate}\" --print filename \"{url}\"");
            var (code, stdout, _) = await RunCaptureAsync(sb.ToString(), ct);
            if (code != 0) return null;
            var line = stdout.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
            if (string.IsNullOrEmpty(line)) return null;

            var path = Path.IsPathRooted(line) ? line : Path.Combine(o.OutputFolder, line);
            var dir = Path.GetDirectoryName(path) ?? o.OutputFolder;
            var name = Path.GetFileNameWithoutExtension(path);
            string ext = o.AudioOnly
                ? (string.Equals(o.AudioFormat, "best", StringComparison.OrdinalIgnoreCase) ? Path.GetExtension(path) : "." + o.AudioFormat)
                : "." + o.EffectiveContainer;
            return Path.Combine(dir, name + ext);
        }
        catch { return null; }
    }

    // Append " (1)", " (2)"… until the path is free.
    private static string Uniquify(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path) ?? "";
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (int i = 1; i < 1000; i++)
        {
            var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
        return path;
    }

    // Pick the real output file. Prefer the pre-resolved exact path; fall back to
    // what yt-dlp reported, then to a same-basename match (extension may differ).
    private static string? PickFinalPath(string? resolved, string? reported, string folder)
    {
        if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved)) return resolved;
        if (!string.IsNullOrWhiteSpace(reported) && Path.IsPathRooted(reported) && File.Exists(reported)) return reported;
        try
        {
            var baseName = Path.GetFileNameWithoutExtension(resolved ?? reported ?? "");
            if (!string.IsNullOrEmpty(baseName) && Directory.Exists(folder))
            {
                var hit = Directory.EnumerateFiles(folder, baseName + ".*")
                    .Where(f => !Regex.IsMatch(f, @"\.f\d+\.[A-Za-z0-9]+$"))
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
                if (hit != null) return hit;
            }
        }
        catch { }
        return resolved ?? folder;
    }

    private async Task<(int code, string stdout, string stderr)> RunCaptureAsync(string args, CancellationToken ct)
    {
        var psi = NewPsi(args);
        using var proc = new Process { StartInfo = psi };
        proc.Start();
        var outTask = proc.StandardOutput.ReadToEndAsync();
        var errTask = proc.StandardError.ReadToEndAsync();
        using (ct.Register(() => { try { if (!proc.HasExited) proc.Kill(true); } catch { } }))
        {
            await proc.WaitForExitAsync(CancellationToken.None);
        }
        return (proc.ExitCode, await outTask, await errTask);
    }

    private ProcessStartInfo NewPsi(string args) => new(_bin.YtDlpPath, args)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8
    };

    // Map a language code to a readable label, e.g. "en" → "English (en)".
    private static string LangDisplay(string code)
    {
        try
        {
            var ci = System.Globalization.CultureInfo.GetCultureInfo(code.Split('-')[0]);
            if (!string.IsNullOrEmpty(ci.EnglishName) && !ci.EnglishName.StartsWith("Unknown"))
                return $"{ci.EnglishName} ({code})";
        }
        catch { }
        return code;
    }

    private static string? GetStr(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string? FirstError(string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr)) return null;
        var lines = stderr.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        if (lines.Count == 0) return null;

        // Prefer an ERROR line that actually carries a message after "ERROR:".
        foreach (var t in lines)
        {
            if (!t.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase)) continue;
            var msg = t.TrimStart();
            int colon = msg.IndexOf(':');
            var detail = colon >= 0 ? msg[(colon + 1)..].Trim() : "";
            if (detail.Length > 0) return Clip(msg);
        }
        // An ERROR line with no detail — append the last informative line.
        var errLine = lines.FirstOrDefault(l => l.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase));
        if (errLine != null)
        {
            var tail = lines.LastOrDefault(l => !l.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase)
                                               && !l.StartsWith("WARNING", StringComparison.OrdinalIgnoreCase));
            return Clip(tail ?? errLine);
        }
        return Clip(lines[^1]);
    }

    private static string Clip(string s) => s.Length > 200 ? s[..200] : s;
}
