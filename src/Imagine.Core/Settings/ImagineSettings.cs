using Imagine.Core.Clock;
using Imagine.Core.Notifications;
using Imagine.Core.Shades;
using Imagine.Core.Theming;

namespace Imagine.Core.Settings;

/// <summary>Все настройки Imagine. Хранятся в %LOCALAPPDATA%\Imagine\settings.json.</summary>
public sealed record ImagineSettings
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;
    public WallpaperSettings Wallpaper { get; init; } = new();
    public ThemeOptions Theme { get; init; } = new();
    public ApplySettings Apply { get; init; } = new();
    public GlassStyle Glass { get; init; } = new();
    public DaySchedule Schedule { get; init; } = new();
    public ShadeSettings Shades { get; init; } = new();
    public NotificationSettings Notifications { get; init; } = new();
    public ClockSettings Clock { get; init; } = new();
    public IntegrationSettings Integrations { get; init; } = new();
    public GeneralSettings General { get; init; } = new();
}

public enum WallpaperFit { Fill, Fit, Stretch, Tile, Center, Span }

public sealed record WallpaperSettings
{
    /// <summary>Путь к обоям для всех мониторов.</summary>
    public string? Path { get; init; }
    /// <summary>Свои обои для отдельных мониторов: ключ — DevicePath монитора.</summary>
    public IReadOnlyDictionary<string, string> PerMonitor { get; init; } = new Dictionary<string, string>();
    public WallpaperFit Fit { get; init; } = WallpaperFit.Fill;
    /// <summary>Папка для слайд-шоу и плитки «Следующие обои».</summary>
    public string? RotationFolder { get; init; }
    public TimeSpan? RotationInterval { get; init; }
    /// <summary>Ставить ту же картинку на экран блокировки.</summary>
    public bool SyncLockScreen { get; init; } = true;
    /// <summary>Рисовать часы прямо в картинку экрана блокировки.</summary>
    public bool BakeClockIntoLockScreen { get; init; }
    /// <summary>Поворачивать «лежащие на боку» снимки автоматически.</summary>
    public bool AutoRotate { get; init; } = true;
}

/// <summary>Куда распространять палитру.</summary>
public sealed record ApplySettings
{
    public bool WindowsAccent { get; init; } = true;
    public bool AccentOnTaskbarAndStart { get; init; }
    public bool AccentOnTitleBars { get; init; } = true;
    public bool WindowsThemeMode { get; init; } = true;
    public bool Transparency { get; init; } = true;
    /// <summary>Подсветка клавиатуры и мыши через Dynamic Lighting.</summary>
    public bool DynamicLighting { get; init; }
    public bool WindowsTerminal { get; init; }
    public bool VsCode { get; init; }
    /// <summary>Пересчитывать палитру по расписанию (утро/вечер).</summary>
    public bool BreathingPalette { get; init; } = true;
    /// <summary>Временно перекрашивать интерфейс под обложку трека.</summary>
    public bool NowPlayingTint { get; init; }
}

public sealed record ShadeSettings
{
    public bool Enabled { get; init; } = true;
    public EdgeSettings Edge { get; init; } = new();
    public string NotificationsHotkey { get; init; } = "Win+Alt+N";
    public string QuickSettingsHotkey { get; init; } = "Win+Alt+A";
    public string ToggleBothHotkey { get; init; } = "Win+Alt+Space";
    /// <summary>Перехватывать штатные Win+N и Win+A (через низкоуровневый хук клавиатуры).</summary>
    public bool ReplaceSystemFlyouts { get; init; }
    public IReadOnlyList<QuickTileSlot> Tiles { get; init; } = QuickTileLayout.Default.Slots;
    public int TileColumns { get; init; } = 4;
    public double Width { get; init; } = 420;
    public bool ShowMediaCard { get; init; } = true;
    public bool ShowBrightness { get; init; } = true;
    public bool ShowVolume { get; init; } = true;
    public bool ShowPerAppVolume { get; init; } = true;
    /// <summary>Затемнять фон под шторкой.</summary>
    public double ScrimOpacity { get; init; } = 0.18;
    /// <summary>Островок сверху экрана: музыка, таймер, загрузки.</summary>
    public bool Island { get; init; } = true;
}

public sealed record NotificationSettings
{
    public NotificationStyle Style { get; init; } = NotificationStyle.Stack;
    public IReadOnlyList<NotificationRule> Rules { get; init; } = [];
    public FocusSchedule Focus { get; init; } = new();
    public bool KeepHistory { get; init; } = true;
    /// <summary>Показывать свои всплывающие капсулы вместо системных баннеров (системные нужно выключить в «Параметрах»).</summary>
    public bool OwnBanners { get; init; }
    public int BannerSeconds { get; init; } = 5;
}

public sealed record ClockSettings
{
    public bool DesktopClock { get; init; } = true;
    public ClockStyle Style { get; init; } = new();
    /// <summary>Анимация «переезда» часов с экрана блокировки после входа.</summary>
    public bool FlyInAfterUnlock { get; init; } = true;
}

public sealed record IntegrationSettings
{
    public string? WindowsTerminalSettingsPath { get; init; }
    public string? VsCodeSettingsPath { get; init; }
    public bool SetTerminalDefaultScheme { get; init; } = true;
}

public sealed record GeneralSettings
{
    public bool StartWithWindows { get; init; } = true;
    public bool ShowTrayIcon { get; init; } = true;
    /// <summary>Перед каждым применением сохранять снимок системных значений для отката.</summary>
    public bool SnapshotBeforeApply { get; init; } = true;
    public string Language { get; init; } = "ru-RU";
}
