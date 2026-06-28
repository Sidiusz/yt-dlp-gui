using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Grabsy.Services;

/// <summary>Owns the locally managed yt-dlp + ffmpeg binaries under
/// %LocalAppData%\Grabsy\bin. Never touches PATH-installed copies.</summary>
public sealed class BinaryManager
{
    private static readonly Lazy<BinaryManager> _instance = new(() => new BinaryManager());
    public static BinaryManager Instance => _instance.Value;

    private const string YtDlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
    private const string YtDlpApiLatest = "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest";
    private const string FfmpegZipUrl = "https://github.com/yt-dlp/FFmpeg-Builds/releases/latest/download/ffmpeg-master-latest-win64-gpl.zip";

    public string BinDir { get; }
    public string YtDlpPath { get; }
    public string FfmpegPath { get; }
    public string FfprobePath { get; }

    private static readonly HttpClient Http = CreateClient();

    private BinaryManager()
    {
        BinDir = Path.Combine(SettingsService.Instance.AppDataDir, "bin");
        Directory.CreateDirectory(BinDir);
        YtDlpPath = Path.Combine(BinDir, "yt-dlp.exe");
        FfmpegPath = Path.Combine(BinDir, "ffmpeg.exe");
        FfprobePath = Path.Combine(BinDir, "ffprobe.exe");
    }

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("Grabsy/0.1");
        return c;
    }

    public bool IsYtDlpInstalled => File.Exists(YtDlpPath);
    public bool IsFfmpegInstalled => File.Exists(FfmpegPath) && File.Exists(FfprobePath);
    public bool IsReady => IsYtDlpInstalled && IsFfmpegInstalled;

    /// <summary>progress: 0..1 fraction, or -1 for indeterminate.</summary>
    public async Task EnsureYtDlpAsync(IProgress<double>? progress = null, CancellationToken ct = default)
    {
        await DownloadFileAsync(YtDlpUrl, YtDlpPath, progress, ct);
    }

    public async Task EnsureFfmpegAsync(IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var tmpZip = Path.Combine(BinDir, "ffmpeg-tmp.zip");
        await DownloadFileAsync(FfmpegZipUrl, tmpZip, progress, ct);
        progress?.Report(-1);
        ExtractFfmpeg(tmpZip);
        try { File.Delete(tmpZip); } catch { }
    }

    public void RemoveYtDlp()
    {
        try { if (File.Exists(YtDlpPath)) File.Delete(YtDlpPath); } catch { }
    }

    public void RemoveFfmpeg()
    {
        try { if (File.Exists(FfmpegPath)) File.Delete(FfmpegPath); } catch { }
        try { if (File.Exists(FfprobePath)) File.Delete(FfprobePath); } catch { }
    }

    // The zip nests binaries under <root>/bin/. Pull just ffmpeg + ffprobe.
    private void ExtractFfmpeg(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in zip.Entries)
        {
            var name = Path.GetFileName(entry.FullName);
            if (string.Equals(name, "ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
                entry.ExtractToFile(FfmpegPath, overwrite: true);
            else if (string.Equals(name, "ffprobe.exe", StringComparison.OrdinalIgnoreCase))
                entry.ExtractToFile(FfprobePath, overwrite: true);
        }
    }

    private static async Task DownloadFileAsync(string url, string dest, IProgress<double>? progress, CancellationToken ct)
    {
        using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength ?? -1L;
        var tmp = dest + ".part";

        await using (var src = await resp.Content.ReadAsStreamAsync(ct))
        await using (var dst = File.Create(tmp))
        {
            var buffer = new byte[81920];
            long read = 0;
            int n;
            while ((n = await src.ReadAsync(buffer, ct)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, n), ct);
                read += n;
                if (total > 0) progress?.Report((double)read / total);
                else progress?.Report(-1);
            }
        }
        if (File.Exists(dest)) File.Delete(dest);
        File.Move(tmp, dest);
    }

    /// <summary>Reads `yt-dlp --version` (date-stamped, e.g. 2024.05.27).</summary>
    public async Task<string?> GetLocalYtDlpVersionAsync()
    {
        if (!IsYtDlpInstalled) return null;
        try
        {
            var psi = new ProcessStartInfo(YtDlpPath, "--version")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi)!;
            var outp = await p.StandardOutput.ReadToEndAsync();
            await p.WaitForExitAsync();
            return outp.Trim();
        }
        catch { return null; }
    }

    public async Task<string?> GetLatestYtDlpVersionAsync(CancellationToken ct = default)
    {
        try
        {
            var json = await Http.GetStringAsync(YtDlpApiLatest, ct);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("tag_name").GetString();
        }
        catch { return null; }
    }

    /// <summary>Self-update via yt-dlp's own updater. Returns final stdout/stderr.</summary>
    public async Task<string> UpdateYtDlpAsync()
    {
        if (!IsYtDlpInstalled)
        {
            await EnsureYtDlpAsync();
            return "Installed yt-dlp.";
        }
        var psi = new ProcessStartInfo(YtDlpPath, "-U")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        var outp = await p.StandardOutput.ReadToEndAsync();
        var err = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        return string.IsNullOrWhiteSpace(outp) ? err.Trim() : outp.Trim();
    }
}
