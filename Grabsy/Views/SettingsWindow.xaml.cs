using System;
using Grabsy.Localization;
using Grabsy.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Graphics;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Grabsy.Views;

public sealed partial class SettingsWindow : Window
{
    private readonly SettingsService _settings = SettingsService.Instance;
    private readonly BinaryManager _bin = BinaryManager.Instance;
    private readonly AppSettings _draft;
    private readonly IntPtr _hwnd;
    private bool _loading;
    private string _theme = "auto";

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public SettingsWindow()
    {
        InitializeComponent();
        ThemeService.Register((FrameworkElement)Content);
        _draft = _settings.Settings.Clone();
        _hwnd = WindowNative.GetWindowHandle(this);

        var appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_hwnd));
        appWindow.Title = "Grabsy — Settings";
        appWindow.Resize(new SizeInt32(940, 640));
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        try
        {
            var tb = appWindow.TitleBar;
            var transparent = Windows.UI.Color.FromArgb(0, 0, 0, 0);
            tb.ButtonBackgroundColor = transparent;
            tb.ButtonInactiveBackgroundColor = transparent;
            tb.ButtonForegroundColor = Windows.UI.Color.FromArgb(0xFF, 0xB5, 0xBA, 0xC1);
            tb.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(0xFF, 0x35, 0x37, 0x3C);
            tb.ButtonHoverForegroundColor = Windows.UI.Color.FromArgb(0xFF, 0xF2, 0xF3, 0xF5);
        }
        catch { }

        VersionLabel.Text = "v" + UpdateService.CurrentVersion();
        try
        {
            var dll = System.Reflection.Assembly.GetExecutingAssembly().Location;
            BuildDateLabel.Text = "Built " + System.IO.File.GetLastWriteTime(dll).ToString("yyyy-MM-dd");
        }
        catch { }

        Load();
        ApplyLocalization();
        Strings.LanguageChanged += ApplyLocalization;
        Closed += (_, _) =>
        {
            Strings.LanguageChanged -= ApplyLocalization;
            // Revert any unsaved language preview back to the persisted choice.
            Strings.Apply(_settings.Settings.Language);
        };
        if (Content is FrameworkElement fe)
            fe.Loaded += (_, _) => { SnapNavVisuals(); RefreshNavIcons(); };
    }

    private void ApplyLocalization()
    {
        SubtitleText.Text = Strings.Get("SettingsSub");
        NavHeaderText.Text = Strings.Get("NavHeader");
        NavGeneralTxt.Text = Strings.Get("NavGeneral");
        NavVideoTxt.Text = Strings.Get("NavVideo");
        NavAudioTxt.Text = Strings.Get("NavAudio");
        NavSubsTxt.Text = Strings.Get("NavSubs");
        NavNotificationsTxt.Text = Strings.Get("NavNotifications");
        NavComponentsTxt.Text = Strings.Get("NavComponents");
        NavAboutTxt.Text = Strings.Get("NavAbout");
        TipLabelText.Text = Strings.Get("TipLabel");
        TipBodyText.Text = Strings.Get("TipText");

        GenTitleText.Text = Strings.Get("GenTitle");
        GenSubText.Text = Strings.Get("GenSub");
        LangTitleText.Text = Strings.Get("LangTitle");
        LangDescText.Text = Strings.Get("LangDesc");
        LangItemAuto.Content = Strings.Get("LangAuto");
        ThemeTitleText.Text = Strings.Get("ThemeTitle");
        ThemeDescText.Text = Strings.Get("ThemeDesc");
        ThemeBtnAuto.Content = Strings.Get("ThemeAuto");
        ThemeDarkTxt.Text = Strings.Get("ThemeDark");
        ThemeLightTxt.Text = Strings.Get("ThemeLight");
        FolderTitleText.Text = Strings.Get("FolderTitle");
        BrowseButton.Content = Strings.Get("Browse");
        AfterTitleText.Text = Strings.Get("AfterTitle");
        AfterDescText.Text = Strings.Get("AfterDesc");
        AfterNothingItem.Content = Strings.Get("AfterNothing");
        AfterOpenFileItem.Content = Strings.Get("AfterOpenFile");
        AfterOpenFolderItem.Content = Strings.Get("AfterOpenFolder");
        AppMgmtText.Text = Strings.Get("AppMgmt");
        AutostartTitleText.Text = Strings.Get("AutostartTitle");
        AutostartDescText.Text = Strings.Get("AutostartDesc");
        CloseTrayTitleText.Text = Strings.Get("CloseTrayTitle");
        CloseTrayDescText.Text = Strings.Get("CloseTrayDesc");
        OverwriteTitle.Text = Strings.Get("OverwriteTitle");
        OverwriteDesc.Text = Strings.Get("OverwriteDesc");
        UpdatesTitleText.Text = Strings.Get("UpdatesTitle");
        AppUpdateStatus.Text = Strings.Get("UpdatesDesc");
        IntHourlyItem.Content = Strings.Get("IntHourly");
        IntDailyItem.Content = Strings.Get("IntDaily");
        IntWeeklyItem.Content = Strings.Get("IntWeekly");
        IntMonthlyItem.Content = Strings.Get("IntMonthly");
        IntNeverItem.Content = Strings.Get("IntNever");
        CheckNowTxt.Text = Strings.Get("CheckNow");

        VidTitleText.Text = Strings.Get("VidTitle");
        VidSubText.Text = Strings.Get("VidSub");
        DefModeTitleText.Text = Strings.Get("DefModeTitle");
        DefModeDescText.Text = Strings.Get("DefModeDesc");
        ModeVideoItem.Content = Strings.Get("ModeVideoItem");
        ModeAudioItem.Content = Strings.Get("ModeAudioItem");
        QualityTitleText.Text = Strings.Get("Quality");
        QualBestItem.Content = Strings.Get("QualBest");
        ContainerTitleText.Text = Strings.Get("Container");
        CodecTitleText.Text = Strings.Get("Codec");
        CodecDescText.Text = Strings.Get("CodecDesc");
        CodecAutoTitle.Text = Strings.Get("CodecAuto");
        CodecAutoDescText.Text = Strings.Get("CodecAutoDesc");
        CodecH264DescText.Text = Strings.Get("CodecH264Desc");
        CodecH265DescText.Text = Strings.Get("CodecH265Desc");
        CodecVp9DescText.Text = Strings.Get("CodecVp9Desc");
        CodecAv1DescText.Text = Strings.Get("CodecAv1Desc");
        EmbedThumbTitleText.Text = Strings.Get("EmbedThumbTitle");
        EmbedThumbDescText.Text = Strings.Get("EmbedThumbDesc");
        EmbedMetaTitleText.Text = Strings.Get("EmbedMetaTitle");
        EmbedMetaDescText.Text = Strings.Get("EmbedMetaDesc");

        AudTitleText.Text = Strings.Get("AudTitle");
        AudSubText.Text = Strings.Get("AudSub");
        FormatTitleText.Text = Strings.Get("Format");
        FormatDescText.Text = Strings.Get("FormatDesc");
        ABestSourceItem.Content = Strings.Get("ABestSource");
        AQualityTitleText.Text = Strings.Get("Quality");

        SubsTitleText.Text = Strings.Get("SubsTitle");
        SubsSubText.Text = Strings.Get("SubsSub");
        PreselectTitleText.Text = Strings.Get("PreselectTitle");
        PreselectDescText.Text = Strings.Get("PreselectDesc");
        SepSrtTitleText.Text = Strings.Get("SepSrtTitle");
        SepSrtDescText.Text = Strings.Get("SepSrtDesc");

        NotifTitleText.Text = Strings.Get("NotifTitle");
        NotifSubText.Text = Strings.Get("NotifSub");
        ShowNotifText.Text = Strings.Get("ShowNotif");
        NotifCompleteText.Text = Strings.Get("NotifComplete");
        NotifErrorsText.Text = Strings.Get("NotifErrors");
        NotifUpdateText.Text = Strings.Get("NotifUpdate");

        CompTitleText.Text = Strings.Get("CompTitle");
        CompSubText.Text = Strings.Get("CompSub");
        UpdateYtDlpButton.Content = Strings.Get("CompCheckUpdates");
        ReinstallFfmpegButton.Content = Strings.Get("CompReinstall");
        FfmpegNote.Text = Strings.Get("FfmpegNote");
        YtBehaviorTitleText.Text = Strings.Get("YtBehaviorTitle");
        YtBehaviorDescText.Text = Strings.Get("YtBehaviorDesc");
        BehAskItem.Content = Strings.Get("BehAsk");
        BehAutoItem.Content = Strings.Get("BehAuto");
        BehNeverItem.Content = Strings.Get("BehNever");
        BridgeTitleText.Text = Strings.Get("BridgeTitle");
        BridgeDescText.Text = Strings.Get("BridgeDesc");
        InstallScriptButton.Content = Strings.Get("InstallScript");
        GetTamperButton.Content = Strings.Get("GetTamper");
        BridgeHintText.Text = Strings.Get("BridgeHint");

        AboutAuthorText.Text = Strings.Get("AboutAuthor");
        AboutSourceText.Text = Strings.Get("AboutSource");
        AboutPoweredText.Text = Strings.Get("AboutPowered");
        AboutPoweredDescText.Text = Strings.Get("AboutPoweredDesc");
        AboutOpen1.Content = Strings.Get("Open");
        AboutOpen2.Content = Strings.Get("Open");

        BtnReset.Content = Strings.Get("Reset");
        BtnClose.Content = Strings.Get("Close");
        BtnSave.Content = Strings.Get("Save");

        _ = RefreshComponentStatusAsync();
    }

    // Live-preview the chosen language; persisted on Save, reverted on Close.
    private void OnLangChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        MarkDirty();
        Strings.Apply(TagOf(LangBox));
    }

    // ---- Nav ----
    private void OnNavChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb || PaneGeneral == null) return;
        var key = rb.Tag as string;
        PaneGeneral.Visibility = Vis(key == "general");
        PaneVideo.Visibility = Vis(key == "video");
        PaneAudio.Visibility = Vis(key == "audio");
        PaneSubs.Visibility = Vis(key == "subs");
        PaneNotifications.Visibility = Vis(key == "notifications");
        PaneComponents.Visibility = Vis(key == "components");
        PaneAbout.Visibility = Vis(key == "about");
        SnapNavVisuals();
        RefreshNavIcons();
    }

    private void RefreshNavIcons()
    {
        string? key = null;
        foreach (var rb in new[] { NavGeneral, NavVideo, NavAudio, NavSubs, NavNotifications, NavComponents, NavAbout })
            if (rb?.IsChecked == true) { key = rb.Tag as string; break; }
        try
        {
            var accent = ThemeService.GetBrush("GrabsyAccentBrush", (FrameworkElement)Content);
            var dim = ThemeService.GetBrush("GrabsyText2Brush", (FrameworkElement)Content);
            IconGeneral.Foreground = key == "general" ? accent : dim;
            IconVideo.Foreground = key == "video" ? accent : dim;
            IconAudio.Foreground = key == "audio" ? accent : dim;
            IconSubs.Foreground = key == "subs" ? accent : dim;
            IconNotifications.Foreground = key == "notifications" ? accent : dim;
            IconComponents.Foreground = key == "components" ? accent : dim;
            IconAbout.Foreground = key == "about" ? accent : dim;
        }
        catch { }
    }

    private void SnapNavVisuals()
    {
        foreach (var rb in new[] { NavGeneral, NavVideo, NavAudio, NavSubs, NavNotifications, NavComponents, NavAbout })
            if (rb != null) VisualStateManager.GoToState(rb, rb.IsChecked == true ? "Checked" : "Unchecked", false);
    }

    private static Visibility Vis(bool v) => v ? Visibility.Visible : Visibility.Collapsed;

    // ---- Theme segment ----
    private void OnThemeSegmentClick(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton tb && tb.Tag is string t) { SetTheme(t); MarkDirty(); LiveApplyTheme(); }
    }
    private void SetTheme(string theme)
    {
        _theme = theme;
        ThemeBtnAuto.IsChecked = theme == "auto";
        ThemeBtnDark.IsChecked = theme == "dark";
        ThemeBtnLight.IsChecked = theme == "light";
    }
    private void LiveApplyTheme()
    {
        // Preview the theme on this window immediately.
        ((FrameworkElement)Content).RequestedTheme = _theme switch
        {
            "dark" => ElementTheme.Dark,
            "light" => ElementTheme.Light,
            _ => ElementTheme.Default
        };
        RefreshNavIcons();
    }

    // ---- Load / Collect ----
    private void Load()
    {
        _loading = true;
        var s = _draft;
        SetTheme(s.Theme);
        SelectByTag(LangBox, s.Language);
        FolderBox.Text = _settings.GetEffectiveDownloadFolder();
        SelectByTag(AfterBox, s.AfterDownloadAction);
        AutostartSwitch.IsChecked = IsAutostartEnabled();
        CloseTraySwitch.IsChecked = s.CloseToTray;
        OverwriteSwitch.IsChecked = s.OverwriteExisting;
        SelectByTag(UpdateIntervalBox, s.AppUpdateInterval);

        SelectByTag(ModeBox, s.PreferredMode);
        SelectByTag(QualityBox, s.PreferredQuality);
        SelectByTag(ContainerBox, s.PreferredContainer);
        CheckCodec(s.PreferredCodec);
        EmbedThumb.IsChecked = s.EmbedThumbnail;
        EmbedMeta.IsChecked = s.EmbedMetadata;

        SelectByTag(AudioFormatBox, s.PreferredAudioFormat);
        SelectByTag(AudioQualityBox, s.PreferredAudioQuality);

        AllSubs.IsChecked = s.AllSubtitleLanguages;
        WriteSubs.IsChecked = s.WriteSubtitles;

        NotifyMaster.IsChecked = s.NotificationsEnabled;
        NotifyComplete.IsChecked = s.NotifyDownloadComplete;
        NotifyError.IsChecked = s.NotifyErrors;
        NotifyUpdate.IsChecked = s.NotifyUpdateAvailable;
        SetNotifySubEnabled(s.NotificationsEnabled);

        SelectByTag(UpdateBehaviorBox, s.YtDlpUpdateBehavior);

        _loading = false;
        SetDirty(false);
    }

    private void Collect()
    {
        var s = _draft;
        s.Theme = _theme;
        s.Language = TagOf(LangBox);
        s.DownloadFolder = string.IsNullOrWhiteSpace(FolderBox.Text) ? null : FolderBox.Text;
        s.AfterDownloadAction = TagOf(AfterBox);
        s.CloseToTray = CloseTraySwitch.IsChecked == true;
        s.OverwriteExisting = OverwriteSwitch.IsChecked == true;
        s.AppUpdateInterval = TagOf(UpdateIntervalBox);

        s.PreferredMode = TagOf(ModeBox);
        s.PreferredQuality = TagOf(QualityBox);
        s.PreferredContainer = TagOf(ContainerBox);
        s.PreferredCodec = CheckedCodec();
        s.EmbedThumbnail = EmbedThumb.IsChecked == true;
        s.EmbedMetadata = EmbedMeta.IsChecked == true;

        s.PreferredAudioFormat = TagOf(AudioFormatBox);
        s.PreferredAudioQuality = TagOf(AudioQualityBox);

        s.AllSubtitleLanguages = AllSubs.IsChecked == true;
        s.WriteSubtitles = WriteSubs.IsChecked == true;

        s.NotificationsEnabled = NotifyMaster.IsChecked == true;
        s.NotifyDownloadComplete = NotifyComplete.IsChecked == true;
        s.NotifyErrors = NotifyError.IsChecked == true;
        s.NotifyUpdateAvailable = NotifyUpdate.IsChecked == true;

        s.YtDlpUpdateBehavior = TagOf(UpdateBehaviorBox);
    }

    // ---- Change tracking ----
    private void OnAnyControlChanged(object sender, RoutedEventArgs e) => MarkDirty();
    private void OnAnyToggleChanged(object sender, RoutedEventArgs e) => MarkDirty();
    private void MarkDirty() { if (!_loading) SetDirty(true); }
    private void SetDirty(bool dirty)
    {
        if (BtnSave != null) BtnSave.IsEnabled = dirty;
        if (FooterStatusText != null) FooterStatusText.Text = dirty ? Strings.Get("Unsaved") : "";
    }

    private void OnNotifyMasterToggled(object sender, RoutedEventArgs e)
    {
        SetNotifySubEnabled(NotifyMaster.IsChecked == true);
        MarkDirty();
    }
    private void SetNotifySubEnabled(bool on)
    {
        NotifySubPanel.Opacity = on ? 1.0 : 0.4;
        NotifySubPanel.IsHitTestVisible = on;
    }

    // ---- Codec radios ----
    private void CheckCodec(string tag)
    {
        CodecAuto.IsChecked = tag == "auto";
        CodecH264.IsChecked = tag == "h264";
        CodecH265.IsChecked = tag == "h265";
        CodecVp9.IsChecked = tag == "vp9";
        CodecAv1.IsChecked = tag == "av1";
        if (!(CodecAuto.IsChecked == true || CodecH264.IsChecked == true || CodecH265.IsChecked == true
              || CodecVp9.IsChecked == true || CodecAv1.IsChecked == true))
            CodecAuto.IsChecked = true;
    }
    private string CheckedCodec()
    {
        if (CodecH264.IsChecked == true) return "h264";
        if (CodecH265.IsChecked == true) return "h265";
        if (CodecVp9.IsChecked == true) return "vp9";
        if (CodecAv1.IsChecked == true) return "av1";
        return "auto";
    }

    // ---- Autostart (HKCU Run key) ----
    private bool IsAutostartEnabled()
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey);
            return k?.GetValue("Grabsy") != null;
        }
        catch { return false; }
    }
    private void ApplyAutostart(bool enable)
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey);
            if (k == null) return;
            if (enable)
            {
                var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exe)) k.SetValue("Grabsy", $"\"{exe}\"");
            }
            else k.DeleteValue("Grabsy", false);
        }
        catch { }
    }

    // ---- App update ----
    private async void OnCheckUpdates(object sender, RoutedEventArgs e)
    {
        BtnCheckNow.IsEnabled = false;
        AppUpdateStatus.Text = "Checking…";
        try
        {
            var result = await UpdateService.CheckLatestAsync();
            _draft.LastAppUpdateCheckUtc = DateTime.UtcNow;
            if (result.Status == UpdateCheckStatus.Found && result.Info != null)
            {
                if (UpdateService.IsNewer(result.Info.Version, UpdateService.CurrentVersion()))
                {
                    AppUpdateStatus.Text = $"Version {result.Info.Version} available — opening download…";
                    UpdateService.OpenDownload(result.Info);   // checking is auto, download is manual
                }
                else AppUpdateStatus.Text = "You are on the latest version.";
            }
            else if (result.Status == UpdateCheckStatus.NoReleases)
                AppUpdateStatus.Text = "No releases published yet.";
            else
                AppUpdateStatus.Text = "Couldn't check (network).";
        }
        catch (Exception ex) { AppUpdateStatus.Text = "Failed: " + ex.Message; }
        finally { BtnCheckNow.IsEnabled = true; }
    }

    // ---- Components ----
    private async System.Threading.Tasks.Task RefreshComponentStatusAsync()
    {
        FfmpegStatus.Text = _bin.IsFfmpegInstalled ? Strings.Get("CompInstalled") : Strings.Get("CompNotInstalled");
        if (_bin.IsYtDlpInstalled)
        {
            var v = await _bin.GetLocalYtDlpVersionAsync();
            YtDlpStatus.Text = string.IsNullOrEmpty(v) ? Strings.Get("CompInstalled") : $"v{v}";
        }
        else YtDlpStatus.Text = Strings.Get("CompNotInstalled");
    }

    private async void OnUpdateYtDlp(object sender, RoutedEventArgs e)
    {
        UpdateYtDlpButton.IsEnabled = false;
        try
        {
            if (!_bin.IsYtDlpInstalled) { YtDlpStatus.Text = "Downloading…"; await _bin.EnsureYtDlpAsync(); }
            else
            {
                YtDlpStatus.Text = "Checking…";
                var local = await _bin.GetLocalYtDlpVersionAsync();
                var latest = await _bin.GetLatestYtDlpVersionAsync();
                if (latest == null) { YtDlpStatus.Text = "Re-downloading latest…"; await _bin.EnsureYtDlpAsync(); }
                else if (!string.IsNullOrEmpty(local) && latest.TrimStart('v') == local) { YtDlpStatus.Text = $"Up to date (version {local})."; return; }
                else { YtDlpStatus.Text = "Updating…"; await _bin.EnsureYtDlpAsync(); }
            }
            await RefreshComponentStatusAsync();
        }
        catch (Exception ex) { YtDlpStatus.Text = "Failed: " + ex.Message; }
        finally { UpdateYtDlpButton.IsEnabled = true; }
    }

    private async void OnReinstallFfmpeg(object sender, RoutedEventArgs e)
    {
        ReinstallFfmpegButton.IsEnabled = false;
        FfmpegStatus.Text = Strings.Get("CompDownloading");
        try { await _bin.EnsureFfmpegAsync(); }
        catch (Exception ex) { FfmpegStatus.Text = "Failed: " + ex.Message; }
        finally { ReinstallFfmpegButton.IsEnabled = true; await RefreshComponentStatusAsync(); }
    }

    private async void OnRemoveYtDlp(object sender, RoutedEventArgs e)
    {
        _bin.RemoveYtDlp();
        await RefreshComponentStatusAsync();
    }

    private async void OnRemoveFfmpeg(object sender, RoutedEventArgs e)
    {
        _bin.RemoveFfmpeg();
        await RefreshComponentStatusAsync();
    }

    // Open the raw .user.js in the default browser; Tampermonkey detects the
    // .user.js URL and shows its native one-click install (and auto-updates).
    private const string UserscriptUrl =
        "https://raw.githubusercontent.com/Sidiusz/yt-dlp-gui/main/Grabsy/Assets/grabsy.user.js";

    private void OnInstallUserscript(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(UserscriptUrl) { UseShellExecute = true }); }
        catch { }
    }

    private void OnGetTampermonkey(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://www.tampermonkey.net/") { UseShellExecute = true }); }
        catch { }
    }

    private async void OnBrowseFolder(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.VideosLibrary };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, _hwnd);
        var folder = await picker.PickSingleFolderAsync();
        if (folder != null) { FolderBox.Text = folder.Path; MarkDirty(); }
    }

    // ---- Footer ----
    private void OnSave(object sender, RoutedEventArgs e)
    {
        Collect();
        ApplyAutostart(AutostartSwitch.IsChecked == true);
        _settings.Replace(_draft);
        ThemeService.ApplyAll();
        Strings.Apply(_draft.Language);
        SetDirty(false);
        if (FooterStatusText != null) FooterStatusText.Text = Strings.Get("SettingsSaved");
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnReset(object sender, RoutedEventArgs e)
    {
        CopyInto(new AppSettings());
        Load();
        LiveApplyTheme();
        SetDirty(true);
    }

    private void CopyInto(AppSettings src)
    {
        foreach (var p in typeof(AppSettings).GetProperties())
            if (p.CanRead && p.CanWrite) p.SetValue(_draft, p.GetValue(src));
    }

    // ---- Helpers ----
    private static void SelectByTag(ComboBox cb, string tag)
    {
        foreach (var o in cb.Items)
            if (o is ComboBoxItem it && (string)it.Tag == tag) { cb.SelectedItem = it; return; }
        if (cb.Items.Count > 0) cb.SelectedIndex = 0;
    }
    private static string TagOf(ComboBox cb) => (cb.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
}
