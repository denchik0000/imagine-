using Imagine.Core.Notifications;
using Imagine.Core.Settings;
using Imagine.Core.Shades;
using Imagine.Core.Theming;

namespace Imagine.Core.Services;

/// <summary>Монитор для обоев.</summary>
public sealed record MonitorInfo(string DevicePath, string FriendlyName, ScreenRect Bounds, bool IsPrimary);

public interface IWallpaperService
{
    IReadOnlyList<MonitorInfo> GetMonitors();
    string? GetWallpaper(string? monitorDevicePath = null);
    void SetWallpaper(string imagePath, string? monitorDevicePath = null, WallpaperFit fit = WallpaperFit.Fill);
    /// <summary>Встроенное слайд-шоу Windows по папке.</summary>
    void SetSlideshow(string folder, TimeSpan interval, bool shuffle);
}

public interface ILockScreenService
{
    /// <summary>Работает только у упакованного приложения (MSIX).</summary>
    bool IsSupported { get; }
    Task SetImageAsync(string imagePath, CancellationToken ct = default);
}

/// <summary>Применение акцента и режима Windows.</summary>
public interface IAccentService
{
    WindowsAccentPalette? ReadCurrent();
    void Apply(WindowsAccentPalette palette, bool showOnTaskbarAndStart, bool showOnTitleBars);
    void SetThemeMode(bool appsDark, bool systemDark);
    void SetTransparency(bool enabled);
    /// <summary>Разослать WM_SETTINGCHANGE, чтобы Проводник и приложения подхватили изменения.</summary>
    void Broadcast();
}

public sealed record WifiNetwork(string Ssid, int SignalBars, bool IsSecured, bool IsConnected);

public interface IRadioService
{
    Task<bool?> GetWifiStateAsync();
    Task<bool> SetWifiAsync(bool on);
    Task<bool?> GetBluetoothStateAsync();
    Task<bool> SetBluetoothAsync(bool on);
    Task<bool> SetAirplaneModeAsync(bool on);
    Task<IReadOnlyList<WifiNetwork>> ScanWifiAsync(CancellationToken ct = default);
    event EventHandler? Changed;
}

public sealed record AudioSessionInfo(string Id, string DisplayName, string? IconPath, float Volume, bool Muted);
public sealed record AudioDeviceInfo(string Id, string Name, bool IsDefault);

public interface IAudioService
{
    float MasterVolume { get; set; }
    bool IsMuted { get; set; }
    bool IsMicrophoneMuted { get; set; }
    IReadOnlyList<AudioSessionInfo> GetSessions();
    void SetSessionVolume(string sessionId, float volume);
    IReadOnlyList<AudioDeviceInfo> GetOutputDevices();
    /// <summary>Сменить устройство вывода (недокументированный IPolicyConfig; может не работать).</summary>
    bool TrySetDefaultOutput(string deviceId);
    event EventHandler? Changed;
}

public sealed record DisplayBrightness(string MonitorId, string Name, int Percent, bool IsInternal);

public interface IBrightnessService
{
    IReadOnlyList<DisplayBrightness> GetDisplays();
    bool SetBrightness(string monitorId, int percent);
}

public sealed record MediaState(
    string SourceAppId,
    string Title,
    string Artist,
    string? AlbumTitle,
    byte[]? Thumbnail,
    bool IsPlaying,
    TimeSpan Position,
    TimeSpan Duration,
    bool CanPrevious,
    bool CanNext);

public interface IMediaService
{
    MediaState? Current { get; }
    event EventHandler<MediaState?>? Changed;
    Task PlayPauseAsync();
    Task NextAsync();
    Task PreviousAsync();
    Task SeekAsync(TimeSpan position);
}

public enum NotificationAccess { Unknown, Allowed, Denied, Unsupported }

public interface INotificationSource
{
    Task<NotificationAccess> RequestAccessAsync();
    Task<IReadOnlyList<NotificationItem>> GetAllAsync();
    void Remove(uint id);
    void ClearAll();
    event EventHandler? Changed;
}

public sealed record BatteryState(int Percent, bool IsCharging, TimeSpan? Remaining, bool BatterySaver);

public interface IPowerService
{
    BatteryState? GetBattery();
    bool SetBatterySaver(bool on);
    /// <summary>Не давать ПК засыпать.</summary>
    void SetKeepAwake(bool on);
    bool IsKeepAwake { get; }
}

/// <summary>Действия, которые запускаются ссылками ms-settings: и т. п.</summary>
public interface ISystemActions
{
    void OpenUri(string uri);
    void TakeScreenshot();
    void LockWorkstation();
    void OpenClipboardHistory();
    void OpenEmojiPanel();
}

/// <summary>RGB-подсветка устройств Dynamic Lighting.</summary>
public interface ILightingService
{
    int DeviceCount { get; }
    Task ApplyAsync(ThemePalette palette);
}
