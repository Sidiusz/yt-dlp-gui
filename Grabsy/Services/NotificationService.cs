using System;
using System.Collections.Generic;
using Microsoft.UI.Dispatching;

namespace Grabsy.Services;

/// <summary>In-app notifications via a self-drawn toast window (Clipsy port).
/// WindowsAppSDK toasts register fine on unpackaged apps but never display, so
/// we draw our own — always reliable.</summary>
public static class NotificationService
{
    public enum Kind { Info, Success, Error, Update }

    private static DispatcherQueue? _dq;
    private static readonly List<Views.ToastWindow> _active = new();   // UI thread only

    // Called once the main window exists; gives us a UI thread to draw toasts on.
    public static void SetHost(DispatcherQueue dq) => _dq = dq;

    // Kept for back-compat with existing call sites; no-ops now.
    public static void Init() { }
    public static void Unregister() { }
    public static void SetTrayFallback(Action<string, string> _) { }

    public static void Show(string title, string body, Kind kind = Kind.Info,
        Action? action = null, string? actionGlyph = null, string? actionTip = null)
    {
        if (!SettingsService.Instance.Settings.NotificationsEnabled) return;
        var dq = _dq;
        if (dq == null) return;
        dq.TryEnqueue(() => ShowOnUi(title, body, kind, action, actionGlyph, actionTip));
    }

    private static void ShowOnUi(string title, string body, Kind kind,
        Action? action, string? actionGlyph, string? actionTip)
    {
        try
        {
            var toast = new Views.ToastWindow(title, body, kind, action, actionGlyph, actionTip);
            toast.Closed += OnToastClosed;
            _active.Add(toast);
            RepositionAll();
        }
        catch { }
    }

    private static void OnToastClosed(object sender, Microsoft.UI.Xaml.WindowEventArgs e)
    {
        if (sender is Views.ToastWindow tw)
        {
            tw.Closed -= OnToastClosed;
            _active.Remove(tw);
            RepositionAll();
        }
    }

    private static void RepositionAll()
    {
        for (int i = 0; i < _active.Count; i++)
            _active[i].PositionAtSlot(i);
    }

    public static void DownloadComplete(string title, string? path = null)
    {
        if (!SettingsService.Instance.Settings.NotifyDownloadComplete) return;
        Action? open = null;
        if (!string.IsNullOrEmpty(path))
            open = () =>
            {
                try
                {
                    if (System.IO.File.Exists(path))
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
                    else
                    {
                        var dir = System.IO.Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(dir)) System.Diagnostics.Process.Start("explorer.exe", $"\"{dir}\"");
                    }
                }
                catch { }
            };
        // E838 = open folder glyph.
        Show("Download complete", title, Kind.Success, open, "", "Open folder");
    }

    public static void DownloadFailed(string title)
    {
        if (!SettingsService.Instance.Settings.NotifyErrors) return;
        Show("Download failed", title, Kind.Error);
    }
}
