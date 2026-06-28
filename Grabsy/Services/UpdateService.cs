using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Grabsy.Services;

public sealed record UpdateInfo(string Version, string Url, string Notes, string? InstallerUrl, string? InstallerName);

public enum UpdateCheckStatus { Found, NoReleases, Failed }

public sealed record UpdateCheckResult(UpdateCheckStatus Status, UpdateInfo? Info);

/// <summary>Checks GitHub releases of the app itself (not yt-dlp). Mirrors the
/// reference Electron updater: cache-busted GitHub Releases API first, then a
/// raw update.json fallback (for when api.github.com is blocked), then the web
/// redirect. Checking is automatic; downloading is manual (open the browser).</summary>
public static class UpdateService
{
    private const string Repo = "Sidiusz/yt-dlp-gui";
    private static readonly string ApiLatestUrl = $"https://api.github.com/repos/{Repo}/releases/latest";
    public static string ReleasesPage => $"https://github.com/{Repo}/releases";

    private static readonly HttpClient _http;

    static UpdateService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"Grabsy/{CurrentVersion()} (+github.com/{Repo})");
    }

    // Append two anti-cache params so GitHub/CDN/proxies never return a stale body.
    private static string Bust(string url)
    {
        var sep = url.Contains('?') ? '&' : '?';
        var rnd = Guid.NewGuid().ToString("N").Substring(0, 8);
        return $"{url}{sep}_t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}&rnd={rnd}";
    }

    private static async Task<string?> FetchTextAsync(string url, string? accept = null)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, Bust(url));
            req.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
            { NoCache = true, NoStore = true, MustRevalidate = true };
            req.Headers.Pragma.ParseAdd("no-cache");
            if (accept != null) req.Headers.Accept.ParseAdd(accept);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return "__404__";
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadAsStringAsync();
        }
        catch { return null; }
    }

    public static async Task<UpdateCheckResult> CheckLatestAsync()
    {
        var apiBody = await FetchTextAsync(ApiLatestUrl, "application/vnd.github+json");
        if (apiBody == "__404__") return new UpdateCheckResult(UpdateCheckStatus.NoReleases, null);
        if (apiBody != null)
        {
            var info = ParseApiRelease(apiBody);
            if (info != null) return new UpdateCheckResult(UpdateCheckStatus.Found, info);
        }
        return new UpdateCheckResult(UpdateCheckStatus.Failed, null);
    }

    private static UpdateInfo? ParseApiRelease(string json)
    {
        try
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
        catch { return null; }
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

    // Manual download: open the .exe asset directly when known, else the releases page.
    public static void OpenDownload(UpdateInfo info)
    {
        var url = !string.IsNullOrEmpty(info.InstallerUrl) ? info.InstallerUrl!
                : !string.IsNullOrEmpty(info.Url) ? info.Url : ReleasesPage;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
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
