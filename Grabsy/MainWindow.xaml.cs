using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grabsy.Localization;
using Grabsy.Models;
using Grabsy.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using WinRT.Interop;

namespace Grabsy;

public sealed partial class MainWindow : Window
{
    private readonly ObservableCollection<DownloadJob> _jobs = new();
    private readonly SettingsService _settings = SettingsService.Instance;
    private readonly BinaryManager _bin = BinaryManager.Instance;
    private readonly YtDlpService _yt = YtDlpService.Instance;

    private AppWindow? _appWindow;
    private VideoInfo? _info;
    private string _mode = "videoaudio";     // videoaudio / audio / videoonly
    private bool AudioMode => _mode == "audio";
    private bool VideoOnly => _mode == "videoonly";
    private readonly List<(CheckBox cb, MediaTrack track)> _audioChecks = new();
    private readonly List<(CheckBox cb, MediaTrack track)> _subChecks = new();

    public MainWindow()
    {
        InitializeComponent();
        ThemeService.Register(Root);
        Strings.Apply(_settings.Settings.Language);
        DownloadsList.ItemsSource = _jobs;
        _jobs.CollectionChanged += (_, _) =>
        {
            EmptyHint.Visibility = _jobs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            RefreshStats();
        };

        SetInfoEnabled(false);   // enabled after a successful probe
        SetupWindow();
        SetupTray();
        StartBridge();
        LoadPreferred();
        RefreshSetupState();
        RefreshStats();
        ApplyLocalization();
        Strings.LanguageChanged += ApplyLocalization;
        _ = CheckAppUpdateOnStartupAsync();
    }

    private void ApplyLocalization()
    {
        UrlLabel.Text = Strings.Get("VideoUrl");
        UrlBox.PlaceholderText = Strings.Get("UrlPlaceholder");
        ToolTipService.SetToolTip(PasteButton, Strings.Get("PasteTip"));
        FetchButton.Content = Strings.Get("Fetch");
        SetupTitleText.Text = Strings.Get("SetupTitle");
        SetupInstallButton.Content = Strings.Get("SetupInstall");
        ModeBoth.Content = Strings.Get("ModeBothMain");
        ModeAudioItem.Content = Strings.Get("ModeAudioMain");
        ModeVideoItem.Content = Strings.Get("ModeVideoMain");
        StatDownloadingLbl.Text = Strings.Get("StatDownloading");
        StatDoneLbl.Text = Strings.Get("StatDone");
        StatErrorsLbl.Text = Strings.Get("StatErrors");
        MoreOptionsHeader.Text = Strings.Get("MoreOptions");
        CodecLabel.Text = Strings.Get("VideoCodec");
        TrimLabel.Text = Strings.Get("TrimRange");
        TrimStart.PlaceholderText = Strings.Get("TrimStartPh");
        TrimEnd.PlaceholderText = Strings.Get("TrimEndPh");
        TrimHelpText.Text = Strings.Get("TrimHelp");
        AudioTracksHeader.Text = Strings.Get("AudioTracks");
        AudioAllButton.Content = Strings.Get("All");
        AudioNoneButton.Content = Strings.Get("None");
        AudioTracksEmpty.Text = Strings.Get("AudioTracksEmpty");
        SubsHeader.Text = Strings.Get("Subtitles");
        SubsAllButton.Content = Strings.Get("All");
        SubsNoneButton.Content = Strings.Get("None");
        SubModeEmbed.Content = Strings.Get("EmbedInVideo");
        SubModeWrite.Content = Strings.Get("SeparateSrt");
        SubtitlesEmpty.Text = Strings.Get("SubtitlesEmpty");
        DownloadButton.Content = Strings.Get("Download");
        DownloadsLabel.Text = Strings.Get("Downloads");
        EmptyHint.Text = Strings.Get("EmptyHint");
        ToolTipService.SetToolTip(SettingsButton, Strings.Get("MainSettingsTip"));
        RefreshSetupState();
    }

    // ---- Browser bridge ----
    private BridgeServer? _bridge;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DownloadJob> _bridgeJobs = new();

    private void StartBridge()
    {
        _bridge = new BridgeServer(BridgeStart, BridgeStatus, BridgeCancel);
        _bridge.Start();
    }

    // Called on the listener thread: cancel the running bridge job.
    private void BridgeCancel(string id)
    {
        if (_bridgeJobs.TryGetValue(id, out var job))
            DispatcherQueue.TryEnqueue(() => { try { job.Cts?.Cancel(); } catch { } });
    }

    // Called on the listener thread: hand off to UI, return the id immediately.
    private string BridgeStart(string url, string mode, string quality)
    {
        var id = Guid.NewGuid().ToString("N");
        DispatcherQueue.TryEnqueue(() => StartBridgeJob(id, url, mode, quality));
        return id;
    }

    private async void StartBridgeJob(string id, string url, string mode, string quality)
    {
        if (!_bin.IsReady)
        {
            _bridgeJobs[id] = new DownloadJob { State = JobState.Failed, Status = "yt-dlp / ffmpeg not installed" };
            return;
        }
        VideoInfo info;
        try { info = await _yt.ProbeAsync(url); }
        catch (Exception ex) { _bridgeJobs[id] = new DownloadJob { State = JobState.Failed, Status = ex.Message }; return; }

        var s = _settings.Settings;
        bool audio = mode == "audio";
        bool vonly = mode == "videoonly";
        var o = new DownloadOptions
        {
            AudioOnly = audio,
            VideoOnly = vonly,
            EmbedThumbnail = s.EmbedThumbnail,
            EmbedMetadata = s.EmbedMetadata,
            ConcurrentFragments = s.ConcurrentFragments,
            FilenameTemplate = s.FilenameTemplate,
            OutputFolder = _settings.GetEffectiveDownloadFolder(),
            Overwrite = s.OverwriteExisting
        };
        if (audio) { o.AudioFormat = s.PreferredAudioFormat; o.AudioQuality = s.PreferredAudioQuality; }
        else { o.Quality = string.IsNullOrEmpty(quality) ? "best" : quality; o.Container = s.PreferredContainer; o.Codec = s.PreferredCodec; }

        var job = new DownloadJob
        {
            Url = url, Title = info.Title, ThumbnailUrl = info.ThumbnailUrl, AudioOnly = audio,
            Format = o.Label, State = JobState.Running, Status = "Starting…", Indeterminate = true
        };
        var cts = new CancellationTokenSource();
        job.Cts = cts;
        _bridgeJobs[id] = job;
        AddJob(job);
        _ = RunJobAsync(job, o, cts.Token);
    }

    // Called on the listener thread: simple property reads are fine.
    private BridgeServer.JobStatus? BridgeStatus(string id)
    {
        if (!_bridgeJobs.TryGetValue(id, out var job)) return null;
        var state = job.State switch
        {
            JobState.Completed => "done",
            JobState.Failed => "error",
            JobState.Canceled => "error",
            _ => "running"
        };
        return new BridgeServer.JobStatus((int)job.ProgressPercent, state, job.Status ?? "");
    }

    // ---- Tray ----
    private bool _exiting;
    private Views.TrayMenuWindow? _trayMenu;

    private void SetupTray()
    {
        try
        {
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "grabsy.ico");
            if (File.Exists(path))
                TrayIcon.IconSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(path));
            TrayIcon.LeftClickCommand = new RelayCommand(ShowFromTray);
            TrayIcon.RightClickCommand = new RelayCommand(ShowTrayMenu);
            TrayIcon.ForceCreate();
            // Reliable fallback when WindowsAppSDK toasts aren't available.
            NotificationService.SetTrayFallback((t, b) =>
            {
                try { TrayIcon.ShowNotification(t, b); } catch { }
            });
        }
        catch { }
    }

    // Custom Clipsy-style tray menu (a real popup window, not a MenuFlyout).
    private void ShowTrayMenu()
    {
        if (_trayMenu == null)
        {
            _trayMenu = new Views.TrayMenuWindow();
            _trayMenu.PasteClicked            += () => Defer(StartQuickDownloadFromClipboard);
            _trayMenu.OpenClicked             += () => Defer(ShowFromTray);
            _trayMenu.OpenVideoFolderClicked  += () => Defer(OpenVideoFolder);
            _trayMenu.SettingsClicked         += () => Defer(OpenSettings);
            _trayMenu.ExitClicked             += () => Defer(ExitApp);
        }
        _trayMenu.ShowAtCursor();
    }

    // Run past the tray's nested message pump to avoid XAML init NRE.
    private void Defer(Action a) => DispatcherQueue.TryEnqueue(() => a());

    private void ExitApp()
    {
        _exiting = true;
        try { _bridge?.Stop(); } catch { }
        try { TrayIcon.Dispose(); } catch { }
        try { _trayMenu?.Close(); } catch { }
        Application.Current.Exit();
    }

    // Check for a newer app release per the configured interval; toast if found.
    private async Task CheckAppUpdateOnStartupAsync()
    {
        var s = _settings.Settings;
        if (!UpdateService.ShouldCheckNow(s.AppUpdateInterval, s.LastAppUpdateCheckUtc)) return;
        try
        {
            var result = await UpdateService.CheckLatestAsync();
            s.LastAppUpdateCheckUtc = DateTime.UtcNow;
            _settings.Save();
            if (result.Status == UpdateCheckStatus.Found && result.Info != null
                && UpdateService.IsNewer(result.Info.Version, UpdateService.CurrentVersion())
                && result.Info.Version != s.SkippedAppVersion
                && s.NotifyUpdateAvailable)
            {
                NotificationService.Show("Grabsy update available", $"Version {result.Info.Version} is ready. Open Settings → About.");
            }
        }
        catch { }
    }

    // ---- Window chrome ----
    private void SetupWindow()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd));
        _appWindow.Title = "Grabsy";

        int w = _settings.Settings.WindowWidth > 0 ? _settings.Settings.WindowWidth : 580;
        int h = _settings.Settings.WindowHeight > 0 ? _settings.Settings.WindowHeight : 740;
        _appWindow.Resize(new SizeInt32(w, h));

        try { _appWindow.SetIcon("Assets\\Icons\\grabsy.ico"); } catch { }

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        try
        {
            var tb = _appWindow.TitleBar;
            var transparent = Windows.UI.Color.FromArgb(0, 0, 0, 0);
            tb.ButtonBackgroundColor = transparent;
            tb.ButtonInactiveBackgroundColor = transparent;
            tb.ButtonForegroundColor = Windows.UI.Color.FromArgb(0xFF, 0xB5, 0xBA, 0xC1);
            tb.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(0xFF, 0x80, 0x84, 0x8E);
            tb.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(0xFF, 0x35, 0x37, 0x3C);
            tb.ButtonHoverForegroundColor = Windows.UI.Color.FromArgb(0xFF, 0xF2, 0xF3, 0xF5);
            tb.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(0xFF, 0x40, 0x42, 0x49);
            tb.ButtonPressedForegroundColor = Windows.UI.Color.FromArgb(0xFF, 0xF2, 0xF3, 0xF5);
        }
        catch { }

        _appWindow.Changed += (s, e) =>
        {
            if (e.DidSizeChange && _appWindow.Size.Width > 0)
            {
                _settings.Settings.WindowWidth = _appWindow.Size.Width;
                _settings.Settings.WindowHeight = _appWindow.Size.Height;
            }
        };

        // Close-to-tray: cancel the close and hide instead of exiting.
        _appWindow.Closing += (s, e) =>
        {
            if (!_exiting && _settings.Settings.CloseToTray)
            {
                e.Cancel = true;
                _appWindow.Hide();
            }
        };
        Closed += (_, _) => { _settings.Save(); if (_exiting) { try { TrayIcon.Dispose(); } catch { } } };
    }

    // ---- Preferred options from settings ----
    private void LoadPreferred()
    {
        var s = _settings.Settings;
        _mode = s.PreferredMode == "audio" ? "audio" : "videoaudio";
        SelectByTag(ModeCombo, _mode);
        SelectByTag(CodecCombo, s.PreferredCodec);
        if (SubModeCombo.SelectedIndex < 0) SubModeCombo.SelectedIndex = s.WriteSubtitles ? 1 : 0;
        PopulateOptionCombos();
    }

    private void PopulateOptionCombos()
    {
        var s = _settings.Settings;
        QualityCombo.Items.Clear();
        ContainerCombo.Items.Clear();

        if (AudioMode)
        {
            AddItem(QualityCombo, "Best source", "best");
            AddItem(QualityCombo, "MP3", "mp3"); AddItem(QualityCombo, "M4A", "m4a");
            AddItem(QualityCombo, "Opus", "opus"); AddItem(QualityCombo, "FLAC", "flac");
            SelectByTag(QualityCombo, s.PreferredAudioFormat);

            AddItem(ContainerCombo, "Best", "best");
            AddItem(ContainerCombo, "320 kbps", "320"); AddItem(ContainerCombo, "256 kbps", "256");
            AddItem(ContainerCombo, "192 kbps", "192"); AddItem(ContainerCombo, "128 kbps", "128");
            SelectByTag(ContainerCombo, s.PreferredAudioQuality);
        }
        else
        {
            int max = _info?.AvailableHeights.FirstOrDefault() ?? int.MaxValue;
            AddItem(QualityCombo, "Best", "best");
            foreach (var (label, val, px) in new[]
            {
                ("2160p (4K)", "2160", 2160), ("1440p (2K)", "1440", 1440),
                ("1080p", "1080", 1080), ("720p", "720", 720), ("480p", "480", 480)
            })
            {
                if (px <= max) AddItem(QualityCombo, label, val);
            }
            SelectByTag(QualityCombo, s.PreferredQuality);
            if (QualityCombo.SelectedIndex < 0) QualityCombo.SelectedIndex = 0;

            AddItem(ContainerCombo, "MP4", "mp4"); AddItem(ContainerCombo, "MKV", "mkv");
            AddItem(ContainerCombo, "WEBM", "webm");
            SelectByTag(ContainerCombo, s.PreferredContainer);
        }
        if (QualityCombo.SelectedIndex < 0) QualityCombo.SelectedIndex = 0;
        if (ContainerCombo.SelectedIndex < 0) ContainerCombo.SelectedIndex = 0;

        // Codec/subs apply to any video mode; audio-track picker only when audio is muxed.
        var videoVis = AudioMode ? Visibility.Collapsed : Visibility.Visible;
        CodecRow.Visibility = videoVis;
        SubsSection.Visibility = videoVis;
        AudioSection.Visibility = _mode == "videoaudio" ? Visibility.Visible : Visibility.Collapsed;
    }

    // Bottom-bar live counters.
    private void RefreshStats()
    {
        if (StatDownloading == null) return;
        StatDownloading.Text = _jobs.Count(j => j.IsRunning).ToString();
        StatDone.Text = _jobs.Count(j => j.State == JobState.Completed).ToString();
        StatErrors.Text = _jobs.Count(j => j.State == JobState.Failed || j.State == JobState.Canceled).ToString();
    }

    private void OnJobChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DownloadJob.State)) RefreshStats();
    }

    private void AddJob(DownloadJob job)
    {
        job.PropertyChanged += OnJobChanged;
        _jobs.Insert(0, job);
        RefreshStats();
    }

    // Border has no IsEnabled (not a Control); dim + block hit-testing instead.
    private void SetInfoEnabled(bool on)
    {
        InfoCard.IsHitTestVisible = on;
        InfoCard.Opacity = on ? 1.0 : 0.5;
    }

    // Build the audio-track + subtitle checkbox lists from a probe result.
    private void PopulateTracks(VideoInfo info)
    {
        _audioChecks.Clear();
        AudioTracksPanel.Children.Clear();
        bool multiAudio = info.HasMultipleAudio;
        AudioTracksPanel.Visibility = multiAudio ? Visibility.Visible : Visibility.Collapsed;
        AudioTracksEmpty.Visibility = multiAudio ? Visibility.Collapsed : Visibility.Visible;
        AudioAllButton.IsEnabled = AudioNoneButton.IsEnabled = multiAudio;
        if (multiAudio)
            foreach (var t in info.AudioTracks)
            {
                var cb = new CheckBox { Content = t.Display, IsChecked = true, MinWidth = 0 };
                AudioTracksPanel.Children.Add(cb);
                _audioChecks.Add((cb, t));
            }

        _subChecks.Clear();
        SubtitlesPanel.Children.Clear();
        bool hasSubs = info.HasSubtitles;
        SubtitlesPanel.Visibility = hasSubs ? Visibility.Visible : Visibility.Collapsed;
        SubtitlesEmpty.Visibility = hasSubs ? Visibility.Collapsed : Visibility.Visible;
        SubsAllButton.IsEnabled = SubsNoneButton.IsEnabled = SubModeCombo.IsEnabled = hasSubs;
        if (hasSubs)
        {
            bool preselectAll = _settings.Settings.AllSubtitleLanguages;
            foreach (var t in info.Subtitles)
            {
                var cb = new CheckBox { Content = t.Display, IsChecked = preselectAll, MinWidth = 0 };
                SubtitlesPanel.Children.Add(cb);
                _subChecks.Add((cb, t));
            }
        }
    }

    private void OnSelectAllAudio(object s, RoutedEventArgs e) { foreach (var (cb, _) in _audioChecks) cb.IsChecked = true; }
    private void OnSelectNoneAudio(object s, RoutedEventArgs e) { foreach (var (cb, _) in _audioChecks) cb.IsChecked = false; }
    private void OnSelectAllSubs(object s, RoutedEventArgs e) { foreach (var (cb, _) in _subChecks) cb.IsChecked = true; }
    private void OnSelectNoneSubs(object s, RoutedEventArgs e) { foreach (var (cb, _) in _subChecks) cb.IsChecked = false; }

    private List<string> SelectedAudio() =>
        _audioChecks.Where(x => x.cb.IsChecked == true).Select(x => x.track.Code).ToList();
    private List<string> SelectedSubs() =>
        _subChecks.Where(x => x.cb.IsChecked == true).Select(x => x.track.Code).ToList();

    private static void AddItem(ComboBox cb, string label, string tag)
        => cb.Items.Add(new ComboBoxItem { Content = label, Tag = tag });

    private static void SelectByTag(ComboBox cb, string tag)
    {
        foreach (var o in cb.Items)
            if (o is ComboBoxItem it && (string)it.Tag == tag) { cb.SelectedItem = it; return; }
    }

    private static string TagOf(ComboBox cb)
        => (cb.SelectedItem as ComboBoxItem)?.Tag as string ?? "";

    // ---- Setup / binaries ----
    private void RefreshSetupState()
    {
        bool ready = _bin.IsReady;
        SetupBanner.Visibility = ready ? Visibility.Collapsed : Visibility.Visible;
        FetchButton.IsEnabled = ready;
        DownloadButton.IsEnabled = ready;
        if (!ready)
        {
            var missing = new System.Collections.Generic.List<string>();
            if (!_bin.IsYtDlpInstalled) missing.Add("yt-dlp");
            if (!_bin.IsFfmpegInstalled) missing.Add("ffmpeg");
            SetupText.Text = string.Format(Strings.Get("SetupNeed"),
                string.Join($" {Strings.Get("And")} ", missing));
        }
    }

    private async void OnInstallClick(object sender, RoutedEventArgs e)
    {
        SetupInstallButton.IsEnabled = false;
        SetupProgress.Visibility = Visibility.Visible;
        var progress = new Progress<double>(p =>
        {
            if (p < 0) { SetupProgress.IsIndeterminate = true; }
            else { SetupProgress.IsIndeterminate = false; SetupProgress.Value = p; }
        });
        try
        {
            if (!_bin.IsYtDlpInstalled)
            {
                SetupStatus.Text = "Downloading yt-dlp…";
                await _bin.EnsureYtDlpAsync(progress);
            }
            if (!_bin.IsFfmpegInstalled)
            {
                SetupStatus.Text = "Downloading ffmpeg…";
                SetupProgress.Value = 0;
                await _bin.EnsureFfmpegAsync(progress);
            }
            SetupStatus.Text = "Ready.";
        }
        catch (Exception ex)
        {
            SetupStatus.Text = "Failed: " + ex.Message;
        }
        finally
        {
            SetupProgress.Visibility = Visibility.Collapsed;
            SetupInstallButton.IsEnabled = true;
            RefreshSetupState();
        }
    }

    // ---- URL fetch ----
    private async void OnPasteClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var data = Clipboard.GetContent();
            if (data.Contains(StandardDataFormats.Text))
            {
                UrlBox.Text = (await data.GetTextAsync()).Trim();
                OnFetchClick(sender, e);
            }
        }
        catch { }
    }

    private void OnUrlKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter) OnFetchClick(sender, e);
    }

    // Invalidate the info panel the moment the URL no longer matches the probed
    // video, so a stale "Download" can't fire on the previous link.
    private void OnUrlChanged(object sender, TextChangedEventArgs e)
    {
        var url = UrlBox.Text.Trim();
        if (_info != null && !string.Equals(url, _info.Url, StringComparison.Ordinal))
            ClearInfo();
        else if (url.Length == 0)
            ClearInfo();
    }

    private void ClearInfo()
    {
        _info = null;
        Thumbnail.Source = null;
        InfoTitle.Text = "";
        InfoUploader.Text = "";
        InfoDuration.Text = "";
        _audioChecks.Clear(); AudioTracksPanel.Children.Clear();
        _subChecks.Clear(); SubtitlesPanel.Children.Clear();
        SetInfoEnabled(false);
    }

    // Pasting a link auto-fetches; the Fetch button stays as a manual fallback.
    private async void OnUrlPaste(object sender, TextControlPasteEventArgs e)
    {
        try
        {
            var data = Clipboard.GetContent();
            if (!data.Contains(StandardDataFormats.Text)) return;
            e.Handled = true;
            UrlBox.Text = (await data.GetTextAsync()).Trim();
            UrlBox.Select(UrlBox.Text.Length, 0);
            OnFetchClick(sender, new RoutedEventArgs());
        }
        catch { }
    }

    private async void OnFetchClick(object sender, RoutedEventArgs e)
    {
        var url = UrlBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(url) || !_bin.IsReady) return;

        FetchError.Visibility = Visibility.Collapsed;
        FetchProgress.Visibility = Visibility.Visible;
        FetchButton.IsEnabled = false;
        try
        {
            _info = await _yt.ProbeAsync(url);
            InfoTitle.Text = _info.Title;
            InfoUploader.Text = _info.Uploader ?? "";
            InfoDuration.Text = _info.DurationText;
            Thumbnail.Source = string.IsNullOrEmpty(_info.ThumbnailUrl)
                ? null
                : new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(_info.ThumbnailUrl));
            PopulateOptionCombos();
            PopulateTracks(_info);
            SetInfoEnabled(true);
        }
        catch (Exception ex)
        {
            FetchError.Text = ex.Message;
            FetchError.Visibility = Visibility.Visible;
            SetInfoEnabled(false);
        }
        finally
        {
            FetchProgress.Visibility = Visibility.Collapsed;
            FetchButton.IsEnabled = _bin.IsReady;
        }
    }

    // ---- Mode dropdown (Video & Audio / Just Audio / Just Video) ----
    private void OnModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (QualityCombo == null) return;   // not built yet during InitializeComponent
        _mode = TagOf(ModeCombo);
        PopulateOptionCombos();
    }

    // ---- Download ----
    private void OnDownloadClick(object sender, RoutedEventArgs e)
    {
        if (_info == null || !_bin.IsReady) return;
        var s = _settings.Settings;
        var folder = _settings.GetEffectiveDownloadFolder();

        var o = new DownloadOptions
        {
            AudioOnly = AudioMode,
            VideoOnly = VideoOnly,
            EmbedThumbnail = s.EmbedThumbnail,
            EmbedMetadata = s.EmbedMetadata,
            ConcurrentFragments = s.ConcurrentFragments,
            FilenameTemplate = s.FilenameTemplate,
            OutputFolder = folder,
            Overwrite = s.OverwriteExisting,
            SectionStart = TrimStart.Text,
            SectionEnd = TrimEnd.Text
        };
        if (AudioMode) { o.AudioFormat = TagOf(QualityCombo); o.AudioQuality = TagOf(ContainerCombo); }
        else
        {
            o.Quality = TagOf(QualityCombo);
            o.Container = TagOf(ContainerCombo);
            o.Codec = TagOf(CodecCombo);
            if (!VideoOnly) o.AudioLanguages = SelectedAudio();
            o.SubtitleLanguages = SelectedSubs();
            var subMode = TagOf(SubModeCombo);
            o.EmbedSubtitles = subMode != "write";
            o.WriteSubtitles = subMode == "write";
        }

        var job = new DownloadJob
        {
            Url = _info.Url,
            Title = _info.Title,
            ThumbnailUrl = _info.ThumbnailUrl,
            AudioOnly = AudioMode,
            Format = o.Label,
            State = JobState.Running,
            Status = "Starting…",
            Indeterminate = true
        };
        var cts = new CancellationTokenSource();
        job.Cts = cts;
        AddJob(job);

        _ = RunJobAsync(job, o, cts.Token);
    }

    // ---- Bottom-bar action: open the download folder ----
    private void OnDockOpenFolder(object sender, RoutedEventArgs e) => OpenVideoFolder();

    // ---- History row actions ----
    private void OnJobDelete(object sender, RoutedEventArgs e)
    {
        if (JobOf(sender) is DownloadJob j) RemoveJob(j);
    }

    private static DownloadJob? JobOf(object sender) => (sender as FrameworkElement)?.DataContext as DownloadJob;

    private void RemoveJob(DownloadJob j)
    {
        j.Cts?.Cancel();
        j.PropertyChanged -= OnJobChanged;
        _jobs.Remove(j);
        RefreshStats();
    }

    private void OpenFolderFor(DownloadJob? j)
    {
        try
        {
            if (j?.OutputPath is string p && File.Exists(p))
                Process.Start("explorer.exe", $"/select,\"{p}\"");
            else
                Process.Start("explorer.exe", $"\"{_settings.GetEffectiveDownloadFolder()}\"");
        }
        catch { }
    }

    // Size + timestamp shown next to a finished download.
    private static void SetSizeDate(DownloadJob job)
    {
        try
        {
            if (job.OutputPath is string p && File.Exists(p))
            {
                long bytes = new FileInfo(p).Length;
                job.SizeDate = $"{HumanSize(bytes)} · {DateTime.Now:dd MMM HH:mm}";
            }
            else job.SizeDate = DateTime.Now.ToString("dd MMM HH:mm");
        }
        catch { }
    }

    private static string HumanSize(long bytes)
    {
        string[] u = { "B", "KB", "MB", "GB" };
        double v = bytes; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return $"{v:0.#} {u[i]}";
    }

    private async Task RunJobAsync(DownloadJob job, DownloadOptions o, CancellationToken ct)
    {
        try
        {
            // Process callbacks run off-thread; marshal job/UI mutations back here.
            void Ui(Action a) => DispatcherQueue.TryEnqueue(() => a());
            await _yt.DownloadAsync(job, o, ct, Ui);
            if (job.State == JobState.Completed) { SetSizeDate(job); RunAfterAction(job); NotificationService.DownloadComplete(job.Title); }
            else if (job.State == JobState.Failed) NotificationService.DownloadFailed(job.Title);
        }
        catch (Exception ex)
        {
            job.State = JobState.Failed;
            job.Status = ex.Message;
        }
    }

    private void RunAfterAction(DownloadJob job)
    {
        var action = _settings.Settings.AfterDownloadAction;
        try
        {
            if (action == "open-folder")
                Process.Start("explorer.exe", $"/select,\"{job.OutputPath}\"");
            else if (action == "open-file" && job.OutputPath != null && File.Exists(job.OutputPath))
                Process.Start(new ProcessStartInfo(job.OutputPath) { UseShellExecute = true });
        }
        catch { }
    }

    // ---- Job row actions ----
    private void OnJobCancel(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is DownloadJob job)
            job.Cts?.Cancel();
    }

    private void OnJobOpen(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not DownloadJob job) return;
        try
        {
            // Play the actual file; if it's gone/moved, reveal it in its folder
            // (never hand explorer a bad path — that drops you in Documents).
            if (!string.IsNullOrEmpty(job.OutputPath) && File.Exists(job.OutputPath))
                Process.Start(new ProcessStartInfo(job.OutputPath) { UseShellExecute = true });
            else
                OpenFolderFor(job);
        }
        catch { }
    }

    // ---- Settings ----
    private void OnSettingsClick(object sender, RoutedEventArgs e) => OpenSettings();

    public void OpenSettings()
    {
        var win = new Views.SettingsWindow();
        win.Closed += (_, _) => { LoadPreferred(); ThemeService.ApplyAll(); RefreshSetupState(); };
        win.Activate();
    }

    public void OpenVideoFolder()
    {
        try { Process.Start("explorer.exe", $"\"{_settings.GetEffectiveDownloadFolder()}\""); } catch { }
    }

    // ---- Tray entry points ----
    public void ShowFromTray()
    {
        _appWindow?.Show();
        Activate();
    }

    public async void StartQuickDownloadFromClipboard()
    {
        try
        {
            var data = Clipboard.GetContent();
            if (!data.Contains(StandardDataFormats.Text)) { NotificationService.Show("Grabsy", "Clipboard has no link."); return; }
            var url = (await data.GetTextAsync())?.Trim();
            if (string.IsNullOrWhiteSpace(url)) { NotificationService.Show("Grabsy", "Clipboard has no link."); return; }
            StartQuickDownload(url);
        }
        catch { NotificationService.Show("Grabsy", "Couldn't read the clipboard."); }
    }

    // Background download using the preferred defaults (no picker UI).
    public async void StartQuickDownload(string url)
    {
        if (!_bin.IsReady) { NotificationService.Show("Grabsy", "yt-dlp / ffmpeg not installed yet. Open the app to set them up."); return; }
        NotificationService.Show("Grabsy", "Fetching link…");
        VideoInfo info;
        try { info = await _yt.ProbeAsync(url); }
        catch (Exception ex) { NotificationService.Show("Grabsy", "Couldn't read that link: " + ex.Message); return; }

        var s = _settings.Settings;
        bool audio = s.PreferredMode == "audio";
        var o = new DownloadOptions
        {
            AudioOnly = audio,
            EmbedThumbnail = s.EmbedThumbnail,
            EmbedMetadata = s.EmbedMetadata,
            ConcurrentFragments = s.ConcurrentFragments,
            FilenameTemplate = s.FilenameTemplate,
            OutputFolder = _settings.GetEffectiveDownloadFolder(),
            Overwrite = s.OverwriteExisting
        };
        if (audio) { o.AudioFormat = s.PreferredAudioFormat; o.AudioQuality = s.PreferredAudioQuality; }
        else
        {
            o.Quality = s.PreferredQuality; o.Container = s.PreferredContainer; o.Codec = s.PreferredCodec;
            if (s.AllSubtitleLanguages && info.HasSubtitles)
            {
                o.SubtitleLanguages = info.Subtitles.Select(t => t.Code).ToList();
                o.EmbedSubtitles = !s.WriteSubtitles; o.WriteSubtitles = s.WriteSubtitles;
            }
        }

        var job = new DownloadJob
        {
            Url = url, Title = info.Title, ThumbnailUrl = info.ThumbnailUrl,
            AudioOnly = audio, Format = o.Label, State = JobState.Running, Status = "Starting…", Indeterminate = true
        };
        var cts = new CancellationTokenSource();
        job.Cts = cts;
        AddJob(job);
        _ = RunJobAsync(job, o, cts.Token);
    }
}
