using System;
using System.Collections.Generic;
using System.Globalization;

namespace Grabsy.Localization;

/// <summary>Lightweight runtime localization. Windows call ApplyLocalization()
/// on load and subscribe to LanguageChanged to re-localize without a restart.</summary>
public static class Strings
{
    // Assigned in the static ctor — _en/_ru field initializers must run first.
    private static Dictionary<string, string> _map;
    private static string _lang = "en";

    static Strings() => _map = _en;

    public static string Lang => _lang;
    public static event Action? LanguageChanged;

    /// <summary>Switch the active language. "auto" follows the system UI culture.</summary>
    public static void Apply(string? lang)
    {
        _lang = Normalize(lang);
        _map = _lang == "ru" ? _ru : _en;
        LanguageChanged?.Invoke();
    }

    public static string Get(string key)
        => _map.TryGetValue(key, out var v) ? v : (_en.TryGetValue(key, out var e) ? e : key);

    private static string Normalize(string? lang) => lang switch
    {
        "ru" => "ru",
        "en" => "en",
        _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            .Equals("ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en"
    };

    private static readonly Dictionary<string, string> _en = new()
    {
        // Tray
        ["TrayPaste"] = "Paste & download",
        ["TrayOpen"] = "Open Grabsy",
        ["TrayOpenVideos"] = "Open video folder",
        ["TraySettings"] = "Settings",
        ["TrayExit"] = "Exit",

        // Main window
        ["MainSettingsTip"] = "Settings",
        ["VideoUrl"] = "VIDEO URL",
        ["UrlPlaceholder"] = "Paste a link from YouTube, X, Reddit, TikTok…",
        ["PasteTip"] = "Paste from clipboard",
        ["Fetch"] = "Fetch",
        ["Downloads"] = "DOWNLOADS",
        ["OpenFolder"] = "Open folder",
        ["EmptyHint"] = "No downloads yet. Paste a link above to get started.",
        ["Download"] = "Download",
        ["ModeBothMain"] = "Video & Audio",
        ["ModeAudioMain"] = "Just Audio",
        ["ModeVideoMain"] = "Just Video",
        ["StatDownloading"] = "downloading",
        ["StatDone"] = "done",
        ["StatErrors"] = "errors",
        ["MoreOptions"] = "More options",
        ["VideoCodec"] = "Video codec",
        ["TrimRange"] = "Trim range",
        ["TrimStartPh"] = "start e.g. 0:30",
        ["TrimEndPh"] = "end e.g. 1:45",
        ["TrimHelp"] = "Leave both empty for the full video. Format: SS, M:SS or H:MM:SS.",
        ["AudioTracks"] = "Audio tracks",
        ["All"] = "All",
        ["None"] = "None",
        ["AudioTracksEmpty"] = "Only one audio track available.",
        ["Subtitles"] = "Subtitles",
        ["EmbedInVideo"] = "Embed in video",
        ["SeparateSrt"] = "Separate .srt",
        ["SubtitlesEmpty"] = "No subtitles available for this video.",
        ["DownloadHintVideo"] = "Video is saved with its best audio track included.",
        ["DownloadHintAudio"] = "Audio is extracted and converted to the chosen format.",
        ["SetupTitle"] = "Components required",
        ["SetupText"] = "Grabsy needs yt-dlp and ffmpeg. They will be downloaded into the app data folder, separate from your system PATH.",
        ["SetupNeed"] = "Grabsy needs {0}. They will be downloaded into the app data folder, separate from your system PATH.",
        ["And"] = "and",
        ["SetupInstall"] = "Download & install",
        ["DockEmptyTitle"] = "No downloads yet",
        ["DockEmptyStatus"] = "Your latest download will appear here.",
        ["CancelTip"] = "Cancel",
        ["OpenLinkTip"] = "Open source link",
        ["RemoveTip"] = "Remove from list",
        ["PlayTip"] = "Play video",

        // Settings — shell
        ["SettingsSub"] = "Settings",
        ["NavHeader"] = "SETTINGS",
        ["NavGeneral"] = "General",
        ["NavVideo"] = "Video",
        ["NavAudio"] = "Audio",
        ["NavSubs"] = "Subtitles",
        ["NavNotifications"] = "Notifications",
        ["NavComponents"] = "Components",
        ["NavAbout"] = "About",
        ["TipLabel"] = "TIP",
        ["TipText"] = "Paste a link in the main window and press Enter to fetch instantly.",
        ["Reset"] = "Reset",
        ["Close"] = "Close",
        ["Save"] = "Save changes",
        ["SettingsSaved"] = "Saved",
        ["Unsaved"] = "Unsaved changes",

        // Settings — General
        ["GenTitle"] = "General",
        ["GenSub"] = "Language, theme, and where files are saved.",
        ["LangTitle"] = "Language",
        ["LangDesc"] = "Auto-detect follows the system language.",
        ["LangAuto"] = "Auto-detect",
        ["ThemeTitle"] = "Theme",
        ["ThemeDesc"] = "Applies instantly, no restart needed.",
        ["ThemeAuto"] = "Auto",
        ["ThemeDark"] = "Dark",
        ["ThemeLight"] = "Light",
        ["FolderTitle"] = "Download folder",
        ["Browse"] = "Browse",
        ["AfterTitle"] = "After-download behavior",
        ["AfterDesc"] = "What Grabsy does right after a download finishes.",
        ["AfterNothing"] = "Do nothing",
        ["AfterOpenFile"] = "Open file",
        ["AfterOpenFolder"] = "Show in folder",
        ["AppMgmt"] = "App management",
        ["AutostartTitle"] = "Start with Windows",
        ["AutostartDesc"] = "Launch Grabsy when you sign in.",
        ["CloseTrayTitle"] = "Minimize to tray on close",
        ["CloseTrayDesc"] = "Closing the window hides Grabsy to the tray instead of exiting.",
        ["OverwriteTitle"] = "Overwrite files with the same name",
        ["OverwriteDesc"] = "When on, a new download replaces an existing file with the same name. When off, it is saved alongside as (1), (2)…",
        ["UpdatesTitle"] = "Updates",
        ["UpdatesDesc"] = "How often Grabsy checks for a new version.",
        ["IntHourly"] = "Hourly",
        ["IntDaily"] = "Daily",
        ["IntWeekly"] = "Weekly",
        ["IntMonthly"] = "Monthly",
        ["IntNever"] = "Never",
        ["CheckNow"] = "Check now",

        // Settings — Video
        ["VidTitle"] = "Video",
        ["VidSub"] = "Defaults pre-selected for every new link.",
        ["DefModeTitle"] = "Default mode",
        ["DefModeDesc"] = "Whether new links start as video or audio-only.",
        ["ModeVideoItem"] = "Video",
        ["ModeAudioItem"] = "Audio only",
        ["Quality"] = "Quality",
        ["QualBest"] = "Best available",
        ["Container"] = "Container",
        ["Codec"] = "Codec",
        ["CodecDesc"] = "How the video is compressed. Affects size, quality and compatibility.",
        ["CodecAuto"] = "Automatic",
        ["CodecAutoDesc"] = "Let yt-dlp pick the best available codec",
        ["CodecH264Desc"] = "Most compatible, hardware-accelerated",
        ["CodecH265Desc"] = "Smaller files, slower to decode on old hardware",
        ["CodecVp9Desc"] = "Web-friendly, open codec",
        ["CodecAv1Desc"] = "Smallest files, newest codec",
        ["EmbedThumbTitle"] = "Embed thumbnail",
        ["EmbedThumbDesc"] = "Save the cover image inside the file.",
        ["EmbedMetaTitle"] = "Embed metadata",
        ["EmbedMetaDesc"] = "Write title, uploader and chapters into the file.",

        // Settings — Audio
        ["AudTitle"] = "Audio",
        ["AudSub"] = "Used when downloading in audio-only mode.",
        ["Format"] = "Format",
        ["FormatDesc"] = "Best source keeps the original audio without re-encoding.",
        ["ABestSource"] = "Best source",

        // Settings — Subtitles
        ["SubsTitle"] = "Subtitles",
        ["SubsSub"] = "Per-video languages are chosen in the main window. These are the defaults.",
        ["PreselectTitle"] = "Pre-select all languages",
        ["PreselectDesc"] = "Tick every available subtitle by default.",
        ["SepSrtTitle"] = "Save as separate .srt",
        ["SepSrtDesc"] = "Off embeds subtitles into the video file.",

        // Settings — Notifications
        ["NotifTitle"] = "Notifications",
        ["NotifSub"] = "Choose which events show a toast.",
        ["ShowNotif"] = "Show notifications",
        ["NotifComplete"] = "Download complete",
        ["NotifErrors"] = "Errors",
        ["NotifUpdate"] = "App update available",

        // Settings — Components
        ["CompTitle"] = "Components",
        ["CompSub"] = "Stored in the app data folder, separate from any yt-dlp on your PATH.",
        ["CompCheckUpdates"] = "Check for updates",
        ["CompReinstall"] = "Reinstall",
        ["FfmpegNote"] = "ffmpeg is required to merge video with audio and to convert formats (MP4/MKV, MP3, VP9, AV1…). Removing it disables those downloads until you reinstall.",
        ["YtBehaviorTitle"] = "yt-dlp update behavior",
        ["YtBehaviorDesc"] = "Whether Grabsy refreshes yt-dlp on its own.",
        ["BehAsk"] = "Ask before updating",
        ["BehAuto"] = "Update automatically",
        ["BehNever"] = "Never check",
        ["CompInstalled"] = "Installed",
        ["CompNotInstalled"] = "Not installed",
        ["CompChecking"] = "Checking…",
        ["CompDownloading"] = "Downloading…",
        ["BridgeTitle"] = "Browser integration",
        ["BridgeDesc"] = "Adds a one-click download button on YouTube and other sites via a Tampermonkey userscript. Keep Grabsy running to receive links.",
        ["InstallScript"] = "Install browser script",
        ["GetTamper"] = "Get Tampermonkey",
        ["BridgeHint"] = "Tampermonkey must be installed first. The script then opens for one-click install.",

        // Settings — About
        ["AboutAuthor"] = "Author",
        ["AboutSource"] = "Source, issues, releases",
        ["AboutPowered"] = "Powered by yt-dlp",
        ["AboutPoweredDesc"] = "The downloader engine this app drives.",
        ["Open"] = "Open",
    };

    private static readonly Dictionary<string, string> _ru = new()
    {
        // Tray
        ["TrayPaste"] = "Вставить и скачать",
        ["TrayOpen"] = "Открыть Grabsy",
        ["TrayOpenVideos"] = "Открыть папку с видео",
        ["TraySettings"] = "Настройки",
        ["TrayExit"] = "Выход",

        // Main window
        ["MainSettingsTip"] = "Настройки",
        ["VideoUrl"] = "ССЫЛКА НА ВИДЕО",
        ["UrlPlaceholder"] = "Вставьте ссылку с YouTube, X, Reddit, TikTok…",
        ["PasteTip"] = "Вставить из буфера обмена",
        ["Fetch"] = "Загрузить",
        ["Downloads"] = "ЗАГРУЗКИ",
        ["OpenFolder"] = "Открыть папку",
        ["EmptyHint"] = "Пока нет загрузок. Вставьте ссылку выше, чтобы начать.",
        ["Download"] = "Скачать",
        ["ModeBothMain"] = "Видео и аудио",
        ["ModeAudioMain"] = "Только аудио",
        ["ModeVideoMain"] = "Только видео",
        ["StatDownloading"] = "качается",
        ["StatDone"] = "скачано",
        ["StatErrors"] = "ошибок",
        ["MoreOptions"] = "Больше параметров",
        ["VideoCodec"] = "Видеокодек",
        ["TrimRange"] = "Обрезка",
        ["TrimStartPh"] = "начало, напр. 0:30",
        ["TrimEndPh"] = "конец, напр. 1:45",
        ["TrimHelp"] = "Оставьте оба поля пустыми для всего видео. Формат: SS, M:SS или H:MM:SS.",
        ["AudioTracks"] = "Аудиодорожки",
        ["All"] = "Все",
        ["None"] = "Нет",
        ["AudioTracksEmpty"] = "Доступна только одна аудиодорожка.",
        ["Subtitles"] = "Субтитры",
        ["EmbedInVideo"] = "Встроить в видео",
        ["SeparateSrt"] = "Отдельный .srt",
        ["SubtitlesEmpty"] = "Для этого видео нет субтитров.",
        ["DownloadHintVideo"] = "Видео сохраняется вместе с лучшей аудиодорожкой.",
        ["DownloadHintAudio"] = "Аудио извлекается и конвертируется в выбранный формат.",
        ["SetupTitle"] = "Требуются компоненты",
        ["SetupText"] = "Grabsy нужны yt-dlp и ffmpeg. Они будут загружены в папку данных приложения, отдельно от системного PATH.",
        ["SetupNeed"] = "Grabsy нужны {0}. Они будут загружены в папку данных приложения, отдельно от системного PATH.",
        ["And"] = "и",
        ["SetupInstall"] = "Скачать и установить",
        ["DockEmptyTitle"] = "Пока нет загрузок",
        ["DockEmptyStatus"] = "Здесь появится последняя загрузка.",
        ["CancelTip"] = "Отменить",
        ["OpenLinkTip"] = "Открыть исходную ссылку",
        ["RemoveTip"] = "Убрать из списка",
        ["PlayTip"] = "Воспроизвести видео",

        // Settings — shell
        ["SettingsSub"] = "Настройки",
        ["NavHeader"] = "НАСТРОЙКИ",
        ["NavGeneral"] = "Общие",
        ["NavVideo"] = "Видео",
        ["NavAudio"] = "Аудио",
        ["NavSubs"] = "Субтитры",
        ["NavNotifications"] = "Уведомления",
        ["NavComponents"] = "Компоненты",
        ["NavAbout"] = "О программе",
        ["TipLabel"] = "СОВЕТ",
        ["TipText"] = "Вставьте ссылку в главном окне и нажмите Enter, чтобы сразу загрузить.",
        ["Reset"] = "Сбросить",
        ["Close"] = "Закрыть",
        ["Save"] = "Сохранить",
        ["SettingsSaved"] = "Сохранено",
        ["Unsaved"] = "Несохранённые изменения",

        // Settings — General
        ["GenTitle"] = "Общие",
        ["GenSub"] = "Язык, тема и папка для сохранения файлов.",
        ["LangTitle"] = "Язык",
        ["LangDesc"] = "Автоопределение следует языку системы.",
        ["LangAuto"] = "Автоопределение",
        ["ThemeTitle"] = "Тема",
        ["ThemeDesc"] = "Применяется сразу, без перезапуска.",
        ["ThemeAuto"] = "Авто",
        ["ThemeDark"] = "Тёмная",
        ["ThemeLight"] = "Светлая",
        ["FolderTitle"] = "Папка для загрузок",
        ["Browse"] = "Обзор",
        ["AfterTitle"] = "Действие после загрузки",
        ["AfterDesc"] = "Что Grabsy делает сразу после завершения загрузки.",
        ["AfterNothing"] = "Ничего",
        ["AfterOpenFile"] = "Открыть файл",
        ["AfterOpenFolder"] = "Показать в папке",
        ["AppMgmt"] = "Управление приложением",
        ["AutostartTitle"] = "Запускать с Windows",
        ["AutostartDesc"] = "Запускать Grabsy при входе в систему.",
        ["CloseTrayTitle"] = "Сворачивать в трей при закрытии",
        ["CloseTrayDesc"] = "Закрытие окна сворачивает Grabsy в трей вместо выхода.",
        ["OverwriteTitle"] = "Перезаписывать файлы с одинаковым именем",
        ["OverwriteDesc"] = "Если включено, новая загрузка заменяет существующий файл с тем же именем. Если выключено — сохраняется рядом как (1), (2)…",
        ["UpdatesTitle"] = "Обновления",
        ["UpdatesDesc"] = "Как часто Grabsy проверяет наличие новой версии.",
        ["IntHourly"] = "Каждый час",
        ["IntDaily"] = "Ежедневно",
        ["IntWeekly"] = "Еженедельно",
        ["IntMonthly"] = "Ежемесячно",
        ["IntNever"] = "Никогда",
        ["CheckNow"] = "Проверить сейчас",

        // Settings — Video
        ["VidTitle"] = "Видео",
        ["VidSub"] = "Параметры по умолчанию для каждой новой ссылки.",
        ["DefModeTitle"] = "Режим по умолчанию",
        ["DefModeDesc"] = "С чего начинать для новых ссылок — видео или только аудио.",
        ["ModeVideoItem"] = "Видео",
        ["ModeAudioItem"] = "Только аудио",
        ["Quality"] = "Качество",
        ["QualBest"] = "Наилучшее",
        ["Container"] = "Контейнер",
        ["Codec"] = "Кодек",
        ["CodecDesc"] = "Как сжимается видео. Влияет на размер, качество и совместимость.",
        ["CodecAuto"] = "Автоматически",
        ["CodecAutoDesc"] = "Позволить yt-dlp выбрать лучший доступный кодек",
        ["CodecH264Desc"] = "Самый совместимый, аппаратное ускорение",
        ["CodecH265Desc"] = "Файлы меньше, медленнее декодируется на старом железе",
        ["CodecVp9Desc"] = "Открытый кодек, удобен для веба",
        ["CodecAv1Desc"] = "Самые маленькие файлы, новейший кодек",
        ["EmbedThumbTitle"] = "Встроить обложку",
        ["EmbedThumbDesc"] = "Сохранять обложку внутри файла.",
        ["EmbedMetaTitle"] = "Встроить метаданные",
        ["EmbedMetaDesc"] = "Записывать название, автора и главы в файл.",

        // Settings — Audio
        ["AudTitle"] = "Аудио",
        ["AudSub"] = "Используется при загрузке только аудио.",
        ["Format"] = "Формат",
        ["FormatDesc"] = "«Наилучший источник» сохраняет оригинальное аудио без перекодирования.",
        ["ABestSource"] = "Наилучший источник",

        // Settings — Subtitles
        ["SubsTitle"] = "Субтитры",
        ["SubsSub"] = "Языки для каждого видео выбираются в главном окне. Это значения по умолчанию.",
        ["PreselectTitle"] = "Заранее выбирать все языки",
        ["PreselectDesc"] = "Отмечать все доступные субтитры по умолчанию.",
        ["SepSrtTitle"] = "Сохранять как отдельный .srt",
        ["SepSrtDesc"] = "Если выключено — субтитры встраиваются в видеофайл.",

        // Settings — Notifications
        ["NotifTitle"] = "Уведомления",
        ["NotifSub"] = "Выберите, какие события показывают уведомление.",
        ["ShowNotif"] = "Показывать уведомления",
        ["NotifComplete"] = "Загрузка завершена",
        ["NotifErrors"] = "Ошибки",
        ["NotifUpdate"] = "Доступно обновление приложения",

        // Settings — Components
        ["CompTitle"] = "Компоненты",
        ["CompSub"] = "Хранятся в папке данных приложения, отдельно от yt-dlp в системном PATH.",
        ["CompCheckUpdates"] = "Проверить обновления",
        ["CompReinstall"] = "Переустановить",
        ["FfmpegNote"] = "ffmpeg нужен для объединения видео со звуком и конвертации форматов (MP4/MKV, MP3, VP9, AV1…). Без него такие загрузки недоступны до повторной установки.",
        ["YtBehaviorTitle"] = "Обновление yt-dlp",
        ["YtBehaviorDesc"] = "Обновлять ли Grabsy yt-dlp самостоятельно.",
        ["BehAsk"] = "Спрашивать перед обновлением",
        ["BehAuto"] = "Обновлять автоматически",
        ["BehNever"] = "Не проверять",
        ["CompInstalled"] = "Установлено",
        ["CompNotInstalled"] = "Не установлено",
        ["CompChecking"] = "Проверка…",
        ["CompDownloading"] = "Загрузка…",
        ["BridgeTitle"] = "Интеграция с браузером",
        ["BridgeDesc"] = "Добавляет кнопку скачивания под видео на YouTube и других сайтах через скрипт Tampermonkey. Grabsy должен быть запущен, чтобы принимать ссылки.",
        ["InstallScript"] = "Установить скрипт",
        ["GetTamper"] = "Установить Tampermonkey",
        ["BridgeHint"] = "Сначала нужен Tampermonkey. Затем скрипт откроется для установки в один клик.",

        // Settings — About
        ["AboutAuthor"] = "Автор",
        ["AboutSource"] = "Исходный код, проблемы, релизы",
        ["AboutPowered"] = "Работает на yt-dlp",
        ["AboutPoweredDesc"] = "Движок загрузчика, которым управляет приложение.",
        ["Open"] = "Открыть",
    };
}
