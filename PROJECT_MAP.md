# Grabsy - Project Map

Read only files relevant to the task.
Use grep/search first. Map is authoritative.

## Root
- `CLAUDE.md` - workflow/rules
- `PROJECT_MAP.md` - this map
- `Grabsy.sln` - solution
- `global.json` - .NET SDK pin (8.0.407)
- `Grabsy/` - app

## Grabsy/ - app root

### Entry points
- `Program.cs` - custom Main, single-instance mutex
- `App.xaml` / `.cs` - app lifecycle, creates MainWindow
- `MainWindow.xaml` / `.cs` - quick-download UI: URL field (auto-fetches on paste, Fetch is fallback); always-visible info+options card (dimmed/non-interactive until probed via SetInfoEnabled — Border has no IsEnabled); mode dropdown Video&Audio/Just Audio/Just Video; quality+container; More options (codec/trim/audio tracks/subs); downloads card (play-on-thumbnail opens the file, filled-red delete only); bottom status bar (downloading/done/errors counters + open-folder + settings); tray icon (left-click opens app, right-click opens custom TrayMenuWindow); close-to-tray; StartQuickDownload; ApplyLocalization (en/ru)
- `app.manifest` - asInvoker, PerMonitorV2 DPI

### Services/
- `SettingsService.cs` - AppSettings model + JSON persistence (LocalAppData\Grabsy)
- `BinaryManager.cs` - download/update/remove yt-dlp + ffmpeg into LocalAppData\Grabsy\bin (never PATH)
- `YtDlpService.cs` - probe (-J) + download with progress parse; DownloadOptions + arg builder (codec sort, trim sections, all-langs, 403 retry flags); pre-resolves exact output path so Play opens the real file
- `UpdateService.cs` - app self-update CHECK only (download is manual): cache-busted GitHub Releases API; OpenDownload opens the .exe asset/releases page in the browser
- `NotificationService.cs` - download/update notifications via a self-drawn ToastWindow (Clipsy port); SetHost(DispatcherQueue) gives it a UI thread; queues/stacks/repositions toasts. (Windows toasts dropped — never displayed unpackaged)
- `BridgeServer.cs` - loopback HTTP listener (127.0.0.1:47821) for the browser userscript: /ping, /config (app's preferred mode+quality), /download?url=&mode=&quality= (background job, returns id), /status?id= (progress/state poll), /cancel?id=
- `RelayCommand.cs` - minimal ICommand (tray left-click)
- `ThemeService.cs` - apply dark/light to registered window roots; GetBrush themed lookup
- `Converters.cs` - StringToBitmap, BoolToVisibility

### Models/
- `Models.cs` - VideoInfo (heights, Subtitles, AudioTracks), MediaTrack, DownloadJob (observable), JobState

### Views/
- `SettingsWindow.xaml` / `.cs` - Clipsy-style left-nav (940x640, sidebar+tip, Reset/Close/Save footer, mini-toggles): General (language/theme/folder/after/app-mgmt), Video, Audio, Subtitles, Notifications, Components (yt-dlp/ffmpeg install/update/remove), About. Save persists without closing; ApplyLocalization (en/ru) with live language preview
- `ToastWindow.xaml` / `.cs` - self-drawn notification toast (layered top-most window, slide-in, auto-dismiss, stacking) ported from Clipsy; optional action button (download-complete toast = Open folder); used by NotificationService
- `TrayMenuWindow.xaml` / `.cs` - custom tray popup ported literally from Clipsy (layered popup, DWM cloak, cursor positioning, fade-in, hover/press rows): Paste & download / Open Grabsy / Open video folder / Settings / Exit

### Localization/
- `Strings.cs` - runtime en/ru string maps; Strings.Apply(lang) + LanguageChanged event; windows call ApplyLocalization on load and on switch

### Themes/
- `Grabsy.Tokens.xaml` - colors/brushes (dark+light), type, radii (amber accent)
- `Grabsy.Styles.xaml` - control styles (GrabsyButton*, GrabsyIconButton/Danger/DangerFilled, GrabsyComboBox, GrabsyGroupCard, GrabsySegmentBtn, GrabsyThumbButton, text styles)

### Assets/
- `Fonts/Onest-VariableFont_wght.ttf` - app font
- `Icons/` - `grabsy.ico` (exe + taskbar + tray) + `grabsy-{16..2048}.png` (titlebars use -32, tray header uses -64) + README; placeholders, replace artwork keeping names
- `grabsy.user.js` - Tampermonkey userscript (v3): "Grabsy" button left of the YouTube like bar (and round button in the Shorts action bar) → popover with mode/quality/Download; no floating button; calls bridge, polls /status, in-page progress toast with real % and Cancel (→/cancel), auto-closes 5s; popover follows its button on scroll; defaults pulled from /config; app-missing → opens GitHub releases; @updateURL/@downloadURL = GitHub raw for auto-update

## Data locations (runtime)
- Settings: `%LocalAppData%\Grabsy\settings.json`
- Binaries: `%LocalAppData%\Grabsy\bin\{yt-dlp,ffmpeg,ffprobe}.exe`

## Where to start
- Download flow - `MainWindow.xaml.cs` → `YtDlpService.DownloadAsync`
- Format/arg mapping - `YtDlpService.BuildArgs`
- Binary install/update - `BinaryManager.cs`
- Preferences - `Views/SettingsWindow.xaml(.cs)` + `Services/SettingsService.cs`
- Theme tokens - `Themes/Grabsy.Tokens.xaml`, `Grabsy.Styles.xaml`

## Not yet built (planned)
- Playlist handling, download queue concurrency limits, history persistence
- Userscript install opens the GitHub raw .user.js URL; requires the repo pushed (push pending)

## Heavy files
1. `Themes/Grabsy.Styles.xaml` (~1200 LOC)
2. `MainWindow.xaml.cs`
3. `Views/SettingsWindow.xaml`
