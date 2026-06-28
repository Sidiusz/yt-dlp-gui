using System;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace Grabsy.Services;

/// <summary>App notifications via WindowsAppSDK toasts, with a tray-balloon
/// fallback (toast registration silently fails on some unpackaged setups).</summary>
public static class NotificationService
{
    private static bool _registered;
    private static Action<string, string>? _trayFallback;

    public static void Init()
    {
        try { AppNotificationManager.Default.Register(); _registered = true; }
        catch { _registered = false; }
    }

    /// <summary>Provide a tray-balloon shower used when toasts are unavailable.</summary>
    public static void SetTrayFallback(Action<string, string> show) => _trayFallback = show;

    public static void Unregister()
    {
        try { if (_registered) AppNotificationManager.Default.Unregister(); } catch { }
    }

    public static void Show(string title, string body)
    {
        if (!SettingsService.Instance.Settings.NotificationsEnabled) return;
        // Tray balloon first: unpackaged toasts register OK but silently never
        // display (no Start-menu AUMID), so the balloon is the reliable path.
        if (_trayFallback != null)
        {
            try { _trayFallback.Invoke(title, body); return; } catch { }
        }
        if (_registered)
        {
            try
            {
                var n = new AppNotificationBuilder().AddText(title).AddText(body).BuildNotification();
                AppNotificationManager.Default.Show(n);
            }
            catch { }
        }
    }

    public static void DownloadComplete(string title)
    {
        if (!SettingsService.Instance.Settings.NotifyDownloadComplete) return;
        Show("Download complete", title);
    }

    public static void DownloadFailed(string title)
    {
        if (!SettingsService.Instance.Settings.NotifyErrors) return;
        Show("Download failed", title);
    }
}
