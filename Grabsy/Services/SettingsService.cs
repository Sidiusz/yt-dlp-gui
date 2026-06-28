using System;
using System.IO;
using System.Text.Json;

namespace Grabsy.Services;

public sealed class AppSettings
{
    // General
    public string Language { get; set; } = "auto";           // auto / en / ru
    public string Theme { get; set; } = "auto";              // auto / dark / light
    public string? DownloadFolder { get; set; }              // null = default Videos\Grabsy

    // Notifications
    public bool NotificationsEnabled { get; set; } = true;
    public bool NotifyDownloadComplete { get; set; } = true;
    public bool NotifyErrors { get; set; } = true;
    public bool NotifyUpdateAvailable { get; set; } = true;

    // Preferred defaults applied to the quick-download flow
    public string PreferredMode { get; set; } = "video";     // video / audio
    public string PreferredQuality { get; set; } = "1080";   // best / 2160 / 1440 / 1080 / 720 / 480
    public string PreferredContainer { get; set; } = "mp4";  // mp4 / mkv / webm
    public string PreferredCodec { get; set; } = "auto";     // auto / h264 / h265 / vp9 / av1
    public string PreferredAudioFormat { get; set; } = "mp3"; // mp3 / m4a / opus / flac / best
    public string PreferredAudioQuality { get; set; } = "192"; // kbps for lossy, or "best"

    // Post-processing
    public bool EmbedThumbnail { get; set; } = true;
    public bool EmbedMetadata { get; set; } = true;
    public bool EmbedSubtitles { get; set; } = false;
    public bool WriteSubtitles { get; set; } = false;
    public string SubtitleLanguages { get; set; } = "en";    // comma list, or "all"
    public bool AllSubtitleLanguages { get; set; } = false;  // grab every available sub language
    public bool AllAudioLanguages { get; set; } = false;     // mux every available audio track (forces mkv)

    // Behavior
    public bool OverwriteExisting { get; set; } = false;     // false = add " (1)" suffix instead of skipping
    public bool CloseToTray { get; set; } = false;          // closing the window hides to tray
    public bool StartDownloadImmediately { get; set; } = false; // skip the format picker, use preferred
    public string FilenameTemplate { get; set; } = "%(title)s.%(ext)s";
    public int ConcurrentFragments { get; set; } = 4;
    public string AfterDownloadAction { get; set; } = "nothing"; // open-file / open-folder / nothing

    // Tooling / updates
    public string YtDlpUpdateBehavior { get; set; } = "ask";  // auto / ask / never
    public DateTime LastUpdateCheckUtc { get; set; } = DateTime.MinValue;
    // App self-update
    public string AppUpdateInterval { get; set; } = "daily";  // hourly / daily / weekly / monthly / never
    public DateTime LastAppUpdateCheckUtc { get; set; } = DateTime.MinValue;
    public string SkippedAppVersion { get; set; } = "";

    // Window
    public int WindowWidth { get; set; } = 0;
    public int WindowHeight { get; set; } = 0;

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}

public sealed class SettingsService
{
    private static readonly Lazy<SettingsService> _instance = new(() => new SettingsService());
    public static SettingsService Instance => _instance.Value;

    private readonly string _path;
    private readonly JsonSerializerOptions _json = new() { WriteIndented = true };
    public AppSettings Settings { get; private set; }

    public event Action? SettingsChanged;

    /// <summary>Per-user root: %LocalAppData%\Grabsy. Holds settings + bin\.</summary>
    public string AppDataDir { get; }

    public string DefaultDownloadFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Grabsy");

    private SettingsService()
    {
        AppDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Grabsy");
        Directory.CreateDirectory(AppDataDir);
        _path = Path.Combine(AppDataDir, "settings.json");
        Settings = Load();
    }

    public string GetEffectiveDownloadFolder()
    {
        var configured = Settings.DownloadFolder;
        if (!string.IsNullOrEmpty(configured))
        {
            try { Directory.CreateDirectory(configured); return configured!; } catch { }
        }
        var fallback = DefaultDownloadFolder;
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path));
                if (s != null) return s;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Grabsy] Settings load failed: {ex.Message}");
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(Settings, _json));
            SettingsChanged?.Invoke();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Grabsy] Settings save failed: {ex.Message}");
        }
    }

    public void Replace(AppSettings updated)
    {
        Settings = updated;
        Save();
    }
}
