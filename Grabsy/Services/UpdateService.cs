using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Grabsy.Services;

public sealed record UpdateInfo(string Version, string Url, string Notes, string? InstallerUrl, string? InstallerName);

public enum UpdateCheckStatus { Found, NoReleases, Failed }

public sealed record UpdateCheckResult(UpdateCheckStatus Status, UpdateInfo? Info);

/// <summary>Checks GitHub releases of the app itself (not yt-dlp).</summary>
public static class UpdateService
{
    private const string Repo = "Sidiusz/yt-dlp-gui";
    private static readonly string ApiLatestUrl = $"https://api.github.com/repos/{Repo}/releases/latest";
    private static readonly string WebLatestUrl = $"https://github.com/{Repo}/releases/latest";
    public static string ReleasesPage => $"https://github.com/{Repo}/releases";

    private static readonly HttpClient _http;

    static UpdateService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"Grabsy/{CurrentVersion()} (+github.com/{Repo})");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public static async Task<UpdateCheckResult> CheckLatestAsync()
    {
        try
        {
            using var resp = await _http.GetAsync(ApiLatestUrl, HttpCompletionOption.ResponseContentRead);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                return new UpdateCheckResult(UpdateCheckStatus.NoReleases, null);
            if (resp.IsSuccessStatusCode)
            {
                var info = ParseApiRelease(await resp.Content.ReadAsStringAsync());
                if (info != null) return new UpdateCheckResult(UpdateCheckStatus.Found, info);
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Grabsy] Update API failed: {ex.Message}"); }

        return await CheckViaWebRedirectAsync();
    }

    private static UpdateInfo? ParseApiRelease(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
        if (string.IsNullOrEmpty(tag)) return null;
        var url = root.TryGetProperty("html_url", out var u) ? u.GetString() ?? "" : "";
        var notes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";

        string? installerUrl = null, installerName = null;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (name == null || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                var dlUrl = asset.TryGetProperty("browser_download_url", out var d) ? d.GetString() : null;
                bool isSetup = name.Contains("setup", StringComparison.OrdinalIgnoreCase);
                if (installerUrl == null || isSetup)
                {
                    installerUrl = dlUrl; installerName = name;
                    if (isSetup) break;
                }
            }
        }
        return new UpdateInfo(tag.TrimStart('v', 'V'), url, notes, installerUrl, installerName);
    }

    private static async Task<UpdateCheckResult> CheckViaWebRedirectAsync()
    {
        try
        {
            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(12) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"Grabsy/{CurrentVersion()}");
            using var resp = await http.GetAsync(WebLatestUrl, HttpCompletionOption.ResponseHeadersRead);
            var location = resp.Headers.Location?.ToString();
            if (string.IsNullOrEmpty(location)) return new UpdateCheckResult(UpdateCheckStatus.Failed, null);
            const string marker = "/releases/tag/";
            int idx = location.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return new UpdateCheckResult(UpdateCheckStatus.NoReleases, null);
            var tag = location[(idx + marker.Length)..].Trim('/');
            if (string.IsNullOrEmpty(tag)) return new UpdateCheckResult(UpdateCheckStatus.NoReleases, null);
            return new UpdateCheckResult(UpdateCheckStatus.Found, new UpdateInfo(tag.TrimStart('v', 'V'), location, "", null, null));
        }
        catch { return new UpdateCheckResult(UpdateCheckStatus.Failed, null); }
    }

    public static bool IsNewer(string remote, string current)
    {
        if (!TryParseVersion(remote, out var r)) return false;
        if (!TryParseVersion(current, out var c)) return true;
        return r > c;
    }

    private static bool TryParseVersion(string s, out Version v)
    {
        v = new Version(0, 0, 0);
        if (string.IsNullOrEmpty(s)) return false;
        var clean = s.TrimStart('v', 'V').Split('-', '+')[0];
        var parts = clean.Split('.');
        try
        {
            int major = parts.Length > 0 ? int.Parse(parts[0]) : 0;
            int minor = parts.Length > 1 ? int.Parse(parts[1]) : 0;
            int build = parts.Length > 2 ? int.Parse(parts[2]) : 0;
            v = new Version(major, minor, build);
            return true;
        }
        catch { return false; }
    }

    public static async Task<bool> DownloadAndLaunchInstallerAsync(UpdateInfo info)
    {
        if (string.IsNullOrEmpty(info.InstallerUrl)) return false;
        try
        {
            var path = Path.Combine(Path.GetTempPath(), $"GrabsySetup-{info.Version}.exe");
            using (var response = await _http.GetAsync(info.InstallerUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                await using var src = await response.Content.ReadAsStreamAsync();
                await using var dst = File.Create(path);
                await src.CopyToAsync(dst);
            }
            var fi = new FileInfo(path);
            if (!fi.Exists || fi.Length < 1024) return false;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
            return true;
        }
        catch { return false; }
    }

    public static string CurrentVersion()
    {
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return v == null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    public static bool ShouldCheckNow(string interval, DateTime lastCheckUtc)
    {
        var span = interval switch
        {
            "hourly" => TimeSpan.FromHours(1),
            "daily" => TimeSpan.FromDays(1),
            "weekly" => TimeSpan.FromDays(7),
            "monthly" => TimeSpan.FromDays(30),
            "never" => TimeSpan.MaxValue,
            _ => TimeSpan.FromDays(1),
        };
        if (span == TimeSpan.MaxValue) return false;
        return DateTime.UtcNow - lastCheckUtc >= span;
    }
}
