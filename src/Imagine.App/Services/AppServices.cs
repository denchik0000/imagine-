using Imagine.Core.Services;
using Imagine.Core.Settings;
using Imagine.Core.Theming;
using Imagine.Platform.Services;
using Microsoft.UI.Dispatching;

namespace Imagine.App;

/// <summary>Корень композиции: все сервисы приложения в одном месте.</summary>
public sealed class AppServices : IDisposable
{
    private readonly DispatcherQueueTimer _scheduleTimer;
    private readonly DispatcherQueueTimer _rotationTimer;
    private readonly DispatcherQueueTimer _lockTimer;
    private DayPhase? _lastPhase;
    private bool _lastDark;

    public AppServices()
    {
        Dispatcher = DispatcherQueue.GetForCurrentThread();
        DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Imagine");
        Directory.CreateDirectory(DataDirectory);

        Settings = new SettingsStore(SettingsStore.DefaultPath);
        Registry = new RegistryAdapter();
        Accent = new AccentService(Registry);
        Wallpapers = new DesktopWallpaperService();
        LockScreen = new LockScreenService();
        Decoder = new WinRtImageDecoder();
        Snapshots = new SnapshotStore(SnapshotStore.DefaultDirectory);
        Lighting = new LightingService();
        Radios = new RadioService();
        Audio = new AudioService();
        Brightness = new BrightnessService();
        Media = new MediaService();
        Notifications = new NotificationSource(Path.Combine(DataDirectory, "icons"));
        Power = new PowerService();
        Actions = new SystemActions();
        Engine = new PersonalizationEngine(Settings, Decoder, Registry, Accent, Lighting, Snapshots, Wallpapers);

        Engine.ThemeChanged += (_, theme) => Dispatcher.TryEnqueue(() => Ui.Palette.Apply(theme));
        Media.Changed += OnMediaChanged;

        // Раз в минуту проверяем фазу суток: «дыхание» палитры и смена светлой/тёмной по расписанию.
        _scheduleTimer = Dispatcher.CreateTimer();
        _scheduleTimer.Interval = TimeSpan.FromMinutes(1);
        _scheduleTimer.Tick += async (_, _) => await OnScheduleTickAsync();

        _lockTimer = Dispatcher.CreateTimer();
        _lockTimer.Interval = TimeSpan.FromMinutes(1);
        _lockTimer.Tick += async (_, _) =>
        {
            try { await BakeLockScreenAsync(); }
            catch (Exception ex) { Log.Error("Часы на экране блокировки", ex); _lockTimer.Stop(); }
        };

        _rotationTimer = Dispatcher.CreateTimer();
        _rotationTimer.Tick += async (_, _) => await NextWallpaperAsync();
        Settings.Changed += (_, _) => Dispatcher.TryEnqueue(ConfigureRotation);
    }

    public DispatcherQueue Dispatcher { get; }
    public string DataDirectory { get; }
    public SettingsStore Settings { get; }
    public RegistryAdapter Registry { get; }
    public AccentService Accent { get; }
    public DesktopWallpaperService Wallpapers { get; }
    public LockScreenService LockScreen { get; }
    public WinRtImageDecoder Decoder { get; }
    public SnapshotStore Snapshots { get; }
    public LightingService Lighting { get; }
    public RadioService Radios { get; }
    public AudioService Audio { get; }
    public BrightnessService Brightness { get; }
    public MediaService Media { get; }
    public NotificationSource Notifications { get; }
    public PowerService Power { get; }
    public SystemActions Actions { get; }
    public PersonalizationEngine Engine { get; }
    public Core.Notifications.NotificationHistory History { get; } = new();

    public ImagineSettings Current => Settings.Current;

    /// <summary>Последний отчёт применения — показываем в настройках, что получилось.</summary>
    public ApplyReport? LastReport { get; private set; }

    public event EventHandler<ApplyReport>? Applied;

    public async Task StartAsync()
    {
        try
        {
            // Первая тема — сразу, без записи в систему: шторки и часы получают цвета обоев.
            var preview = await Engine.PreviewAsync();
            Ui.Palette.Apply(preview);
            _lastDark = preview.IsDark;
            _lastPhase = Current.Schedule.PhaseAt(DateTimeOffset.Now);
            await Media.InitializeAsync();
            _scheduleTimer.Start();
            ConfigureRotation();
            if (Current.General.StartWithWindows) StartupService.SetEnabled(true, Environment.ProcessPath ?? "");
        }
        catch (Exception ex)
        {
            Log.Error("Запуск сервисов", ex);
        }
    }

    public async Task<ApplyReport?> ApplyAsync(string reason = "Применение темы")
    {
        try
        {
            var report = await Engine.ApplyAsync(reason);
            LastReport = report;
            _lastDark = report.Theme.IsDark;
            Dispatcher.TryEnqueue(() => Applied?.Invoke(this, report));
            foreach (var f in report.Failed) Log.Info("Не применено: " + f);
            return report;
        }
        catch (Exception ex)
        {
            Log.Error("Применение темы", ex);
            return null;
        }
    }

    public async Task<ApplyReport?> SetWallpaperAsync(string path)
    {
        try
        {
            if (Current.Wallpaper.AutoRotate)
            {
                var area = Platform.Windowing.Monitors.Primary();
                int w = area?.Bounds.Width ?? 1920, h = area?.Bounds.Height ?? 1080;
                var source = path;
                path = await Task.Run(() => WallpaperPreparer.Prepare(source, Path.Combine(DataDirectory, "wallpapers"), w, h));
            }
            var report = await Engine.SetWallpaperAsync(path, LockScreen);
            LastReport = report;
            if (Current.Wallpaper.BakeClockIntoLockScreen) await BakeLockScreenAsync();
            Dispatcher.TryEnqueue(() => Applied?.Invoke(this, report));
            return report;
        }
        catch (Exception ex)
        {
            Log.Error("Смена обоев", ex);
            return null;
        }
    }

    /// <summary>
    /// Часы прямо на картинке экрана блокировки: системные часы Windows нельзя перекрасить,
    /// поэтому рисуем свои в стиле Imagine (системные можно скрыть в параметрах экрана блокировки).
    /// </summary>
    public async Task BakeLockScreenAsync()
    {
        var s = Current;
        if (s.Wallpaper.Path is not { } wp || !File.Exists(wp) || !LockScreen.IsSupported || Engine.CurrentTheme is not { } theme) return;
        var area = Platform.Windowing.Monitors.Primary();
        int w = area?.Bounds.Width ?? 1920, h = area?.Bounds.Height ?? 1080;
        var color = theme[s.Clock.Style.ColorRole ?? Imagine.Color.ColorRole.PrimaryFixed];
        var composed = await Task.Run(() => LockScreenComposer.Compose(wp, Path.Combine(DataDirectory, "lockscreen"), s.Clock.Style, color, DateTime.Now, w, h));
        await LockScreen.SetImageAsync(composed);
    }

    public void UpdateSettings(Func<ImagineSettings, ImagineSettings> change) => Settings.Update(change);

    /// <summary>Изменили что-то, что влияет на тему: пересчитать превью сразу, а применить к системе — с небольшой задержкой.</summary>
    public void ThemeSettingsChanged(Func<ImagineSettings, ImagineSettings> change)
    {
        Settings.Update(change);
        _debounce?.Cancel();
        var cts = _debounce = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(450, cts.Token);
                var preview = await Engine.PreviewAsync(ct: cts.Token);
                Dispatcher.TryEnqueue(() => Ui.Palette.Apply(preview));
                await Task.Delay(600, cts.Token);
                await ApplyAsync("Изменение настроек темы");
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    private CancellationTokenSource? _debounce;

    public async Task NextWallpaperAsync()
    {
        var folder = Current.Wallpaper.RotationFolder;
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
        string[] ext = [".jpg", ".jpeg", ".png", ".bmp", ".webp", ".jxr"];
        var files = Directory.EnumerateFiles(folder).Where(f => ext.Contains(Path.GetExtension(f).ToLowerInvariant())).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        if (files.Count == 0) return;
        int i = files.FindIndex(f => string.Equals(f, Current.Wallpaper.Path, StringComparison.OrdinalIgnoreCase));
        await SetWallpaperAsync(files[(i + 1) % files.Count]);
    }

    private void ConfigureRotation()
    {
        // Часы, «вживлённые» в картинку экрана блокировки, перерисовываем раз в минуту (экспериментально).
        if (Current.Wallpaper.BakeClockIntoLockScreen && LockScreen.IsSupported) { if (!_lockTimer.IsRunning) _lockTimer.Start(); }
        else _lockTimer.Stop();

        var interval = Current.Wallpaper.RotationInterval;
        if (interval is { } iv && iv >= TimeSpan.FromMinutes(1) && !string.IsNullOrEmpty(Current.Wallpaper.RotationFolder))
        {
            if (_rotationTimer.Interval != iv || !_rotationTimer.IsRunning)
            {
                _rotationTimer.Interval = iv;
                _rotationTimer.Start();
            }
        }
        else _rotationTimer.Stop();
    }

    private async Task OnScheduleTickAsync()
    {
        var s = Current;
        var phase = s.Schedule.PhaseAt(DateTimeOffset.Now);
        if (phase == _lastPhase) return;
        _lastPhase = phase;
        bool needsDarkSwitch = s.Theme.Mode == ThemeModePreference.BySchedule && DaySchedule.IsDarkPhase(phase) != _lastDark;
        if (needsDarkSwitch || s.Apply.BreathingPalette)
            await ApplyAsync($"Расписание: {phase}");
    }

    private async void OnMediaChanged(object? sender, MediaState? state)
    {
        if (!Current.Apply.NowPlayingTint) return;
        try
        {
            uint? seed = null;
            if (state?.Thumbnail is { Length: > 0 } bytes)
            {
                var pixels = await Decoder.DecodeBytesAsync(bytes, 96);
                seed = WallpaperAnalyzer.Analyze(pixels).Seed;
            }
            if (seed == Engine.NowPlayingSeed) return;
            Engine.NowPlayingSeed = seed;
            var preview = await Engine.PreviewAsync();
            Dispatcher.TryEnqueue(() => Ui.Palette.Apply(preview));
        }
        catch (Exception ex)
        {
            Log.Error("Цвет обложки", ex);
        }
    }

    public void Dispose()
    {
        _scheduleTimer.Stop();
        _rotationTimer.Stop();
        _lockTimer.Stop();
        Audio.Dispose();
        Notifications.Dispose();
    }
}
