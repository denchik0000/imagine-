using Imagine.App.Ui;
using Imagine.Core.Hotkeys;
using Imagine.Core.Shades;
using Imagine.Core.Theming;
using Imagine.Platform.Input;
using Imagine.Platform.Windowing;

namespace Imagine.App.Shell;

/// <summary>
/// Связывает ввод (край экрана, колесо/тачпад, горячие клавиши, трей) с шторками, часами и островком.
/// </summary>
public sealed class ShellController : IDisposable
{
    private readonly AppServices _services;
    private readonly LowLevelKeyboardHook _keyboard = new();
    private readonly object _gate = new();
    private HookThread? _hooks;
    private bool _hooksMouse;
    private bool _hooksKeyboard;
    private bool _keyboardNeeded;
    private readonly MessageWindow _messages = new();
    private readonly EdgeZoneDetector _detector;
    private readonly DispatcherTimerLite _tick;
    private readonly DispatcherTimerLite _fullscreenPoll;
    private NotificationShade? _notifications;
    private QuickSettingsShade? _quick;
    private ClockWidgetWindow? _clock;
    private IslandWindow? _island;
    private TrayIcon? _tray;
    private volatile bool _picking;

    public ShellController(AppServices services)
    {
        _services = services;
        _detector = new EdgeZoneDetector(services.Current.Shades.Edge, Monitors.All().Select(a => a.Bounds));
        // Детектор вызывается из потока хуков, поэтому открываем шторку через очередь интерфейса.
        _detector.Triggered += (_, r) => services.Dispatcher.TryEnqueue(() => Open(r.Kind, r.Trigger, r.AnchorX));
        _tick = new DispatcherTimerLite(services.Dispatcher, TimeSpan.FromMilliseconds(50), OnTick);
        _fullscreenPoll = new DispatcherTimerLite(services.Dispatcher, TimeSpan.FromSeconds(1), () =>
        {
            bool fs = FullscreenDetector.IsFullscreenActive();
            lock (_gate) _detector.IsFullscreenAppActive = fs;
        });

        _messages.DisplayChanged += (_, _) =>
        {
            var bounds = Monitors.All().Select(a => a.Bounds).ToList();
            lock (_gate) _detector.SetMonitors(bounds);
        };
        _messages.SessionUnlocked += (_, _) => services.Dispatcher.TryEnqueue(() => _clock?.FlyIn());
        _messages.SessionLocked += (_, _) => services.Dispatcher.TryEnqueue(CloseAll);
        _messages.SettingChanged += (_, area) =>
        {
            // Пользователь сменил обои средствами Windows — перекрашиваемся сами.
            if (area is null && services.Current.Wallpaper.Path is { } p && services.Wallpapers.GetWallpaper() is { } wp
                && !string.Equals(p, wp, StringComparison.OrdinalIgnoreCase) && File.Exists(wp))
                services.Dispatcher.TryEnqueue(async () => await services.SetWallpaperAsync(wp));
        };
        services.Settings.Changed += (_, s) => services.Dispatcher.TryEnqueue(Reconfigure);
        services.Applied += (_, report) =>
        {
            if (report.Failed.Count == 0) Island.ShowMessage(Glyphs.Palette, $"Тема применена · {Imagine.Color.ColorUtils.ToHex(report.Theme.Seed)}");
            else Island.ShowMessage(Glyphs.Info, $"Применено частично: {report.Failed.Count} ошибок");
        };
    }

    public IslandWindow Island => _island ??= new IslandWindow(_services);

    private NotificationShade Notifications => _notifications ??= CreateShade(new NotificationShade(_services));

    private QuickSettingsShade Quick => _quick ??= CreateShade(new QuickSettingsShade(_services));

    private T CreateShade<T>(T shade) where T : ShadeWindow
    {
        shade.Opened += (_, _) => { lock (_gate) _detector.IsShadeOpen = true; };
        shade.ClosedShade += (_, _) =>
        {
            bool open = (_notifications?.IsOpen ?? false) || (_quick?.IsOpen ?? false);
            lock (_gate)
            {
                _detector.IsShadeOpen = open;
                _detector.NotifyClosed(Environment.TickCount64);
            }
        };
        return shade;
    }

    public void Start()
    {
        Reconfigure();
        if (_services.Current.General.ShowTrayIcon) CreateTray();
        _clock = new ClockWidgetWindow(_services);
        _services.Dispatcher.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            try { _clock.ShowClock(); }
            catch (Exception ex) { Log.Error("Часы на рабочем столе", ex); }
        });
    }

    private void CreateTray()
    {
        try
        {
            var icon = Path.Combine(AppContext.BaseDirectory, "Assets", "Imagine.ico");
            _tray = new TrayIcon(icon, "Imagine", TrayMenu);
            _tray.Clicked += (_, _) => _services.Dispatcher.TryEnqueue(() => Open(ShadeKind.QuickSettings, ShadeTrigger.Tray));
        }
        catch (Exception ex)
        {
            Log.Error("Значок в трее", ex);
        }
    }

    private IReadOnlyList<TrayMenuItem> TrayMenu()
    {
        var s = _services.Current;
        return
        [
            new("Открыть Imagine", () => App.Current.ShowSettings()),
            TrayMenuItem.Separator,
            new("Уведомления\t" + s.Shades.NotificationsHotkey, () => Open(ShadeKind.Notifications, ShadeTrigger.Tray)),
            new("Быстрые настройки\t" + s.Shades.QuickSettingsHotkey, () => Open(ShadeKind.QuickSettings, ShadeTrigger.Tray)),
            TrayMenuItem.Separator,
            new("Применить тему заново", () => _ = _services.ApplyAsync("Из трея")),
            new("Другой цвет с обоев", () =>
            {
                int count = _services.Engine.CurrentAnalysis?.SeedCandidates.Count ?? 1;
                _services.ThemeSettingsChanged(x => x with { Theme = x.Theme with { SeedSource = SeedSource.Wallpaper, WallpaperSeedIndex = (x.Theme.WallpaperSeedIndex + 1) % Math.Max(1, count) } });
            }),
            new("Цвет с экрана (пипетка)", BeginColorPick),
            new("Следующие обои", () => _ = _services.NextWallpaperAsync(), Enabled: !string.IsNullOrEmpty(s.Wallpaper.RotationFolder)),
            TrayMenuItem.Separator,
            new("Шторки", () => _services.UpdateSettings(x => x with { Shades = x.Shades with { Enabled = !x.Shades.Enabled } }), s.Shades.Enabled),
            new("Часы на рабочем столе", () => _services.UpdateSettings(x => x with { Clock = x.Clock with { DesktopClock = !x.Clock.DesktopClock } }), s.Clock.DesktopClock),
            new("Фокус", () => _services.UpdateSettings(x => x with { Notifications = x.Notifications with { Focus = x.Notifications.Focus with { ManualOn = !x.Notifications.Focus.ManualOn } } }), s.Notifications.Focus.ManualOn),
            TrayMenuItem.Separator,
            new("Вернуть всё как было до Imagine", () => { if (_services.Engine.Restore()) Island.ShowMessage(Glyphs.Undo, "Системные цвета восстановлены"); }),
            new("Выход", () => App.Current.Quit()),
        ];
    }

    private ImagineSettingsSnapshot? _applied;

    /// <summary>Снимок того, что влияет на ввод и окна: перенастраиваемся, только если он поменялся.</summary>
    private sealed record ImagineSettingsSnapshot(Core.Settings.ShadeSettings Shades, bool Tray, bool Clock);

    private void Reconfigure()
    {
        var s = _services.Current;
        var snap = new ImagineSettingsSnapshot(s.Shades, s.General.ShowTrayIcon, s.Clock.DesktopClock);
        if (snap == _applied && !_picking) return;
        bool shadesChanged = _applied is null || !Equals(_applied.Shades, s.Shades);
        _applied = snap;

        if (shadesChanged)
        {
            lock (_gate) _detector.Settings = s.Shades.Edge;
            _messages.UnregisterAllHotkeys();
            _keyboard.Clear();
            _keyboardNeeded = false;
            if (s.Shades.Enabled)
            {
                Bind(s.Shades.NotificationsHotkey, () => Toggle(ShadeKind.Notifications));
                Bind(s.Shades.QuickSettingsHotkey, () => Toggle(ShadeKind.QuickSettings));
                Bind(s.Shades.ToggleBothHotkey, ToggleBoth);
            }
            else CloseAll();
        }

        bool edge = s.Shades.Enabled && (s.Shades.Edge.MouseEnabled || s.Shades.Edge.TouchpadPullEnabled);
        UpdateHooks(edge || _picking, _keyboardNeeded);
        if (edge) _tick.Start(); else _tick.Stop();
        if (edge && s.Shades.Edge.DisableInFullscreen) _fullscreenPoll.Start();
        else
        {
            _fullscreenPoll.Stop();
            lock (_gate) _detector.IsFullscreenAppActive = false;
        }

        if (s.General.ShowTrayIcon && _tray is null) CreateTray();
        else if (!s.General.ShowTrayIcon && _tray is not null) { _tray.Dispose(); _tray = null; }
        if (_clock is not null)
        {
            if (s.Clock.DesktopClock && !_clock.IsVisible) _clock.ShowClock();
            else if (!s.Clock.DesktopClock && _clock.IsVisible) _clock.HideClock();
        }
    }

    /// <summary>Хуки живут в отдельном потоке; пересоздаём его, только когда набор хуков меняется.</summary>
    private void UpdateHooks(bool mouse, bool keyboard)
    {
        if (_hooks is not null && _hooksMouse == mouse && _hooksKeyboard == keyboard) return;
        _hooks?.Dispose();
        _hooks = null;
        _hooksMouse = mouse;
        _hooksKeyboard = keyboard;
        if (!mouse && !keyboard) return;
        _hooks = new HookThread(mouse, keyboard ? _keyboard : null);
        if (_hooks.Mouse is { } m) m.Event += OnMouse;
        if (_hooks.Error is { } err) Log.Error("Хуки ввода", err);
    }

    /// <summary>
    /// Сначала обычная системная горячая клавиша; если сочетание занято (Win+… часто резервирует Windows),
    /// перехватываем его хуком клавиатуры.
    /// </summary>
    private void Bind(string text, Action action)
    {
        if (!HotkeyGesture.TryParse(text, out var g) || g.IsEmpty) return;
        if (_messages.RegisterHotkey(g, () => _services.Dispatcher.TryEnqueue(() => action()))) return;
        _keyboard.Intercept(g, () => _services.Dispatcher.TryEnqueue(() => action()));
        _keyboardNeeded = true;
    }

    /// <summary>Вызывается в потоке хуков: только быстрые вычисления, никаких окон.</summary>
    private void OnMouse(MouseHookEvent e)
    {
        if (_picking)
        {
            if (e.IsClick)
            {
                _picking = false;
                int x = e.X, y = e.Y;
                _services.Dispatcher.TryEnqueue(() => FinishColorPick(x, y));
            }
            return;
        }
        if (e.IsClick) return;
        lock (_gate)
        {
            if (e.IsWheel) _detector.OnWheel(e.X, e.Y, e.WheelDelta, e.Timestamp, e.LeftButtonDown);
            else _detector.OnMouseMove(e.X, e.Y, e.Timestamp, e.LeftButtonDown);
        }
    }

    private void OnTick()
    {
        var (x, y) = Monitors.CursorPosition();
        lock (_gate) _detector.OnTick(Environment.TickCount64, x, y);
    }

    public void Open(ShadeKind kind, ShadeTrigger trigger, double? anchorX = null)
    {
        if (!_services.Current.Shades.Enabled && trigger != ShadeTrigger.Tray) return;
        var (cx, _) = Monitors.CursorPosition();
        var request = new ShadeRequest(kind, trigger, anchorX ?? cx);
        // Как в Android: одновременно открыта одна шторка, вторая закрывается.
        if (kind == ShadeKind.Notifications) { _quick?.Close(true); Notifications.Open(request); }
        else { _notifications?.Close(true); Quick.Open(request); }
    }

    private void Toggle(ShadeKind kind)
    {
        var shade = kind == ShadeKind.Notifications ? (ShadeWindow?)_notifications : _quick;
        if (shade is { IsOpen: true }) shade.Close(true);
        else Open(kind, ShadeTrigger.Hotkey);
    }

    private void ToggleBoth()
    {
        if ((_notifications?.IsOpen ?? false) || (_quick?.IsOpen ?? false))
        {
            CloseAll();
            return;
        }
        var (cx, _) = Monitors.CursorPosition();
        Notifications.Open(new ShadeRequest(ShadeKind.Notifications, ShadeTrigger.Hotkey, cx));
        Quick.Open(new ShadeRequest(ShadeKind.QuickSettings, ShadeTrigger.Hotkey, cx));
    }

    public void CloseAll()
    {
        _notifications?.Close(true);
        _quick?.Close(true);
    }

    /// <summary>Пипетка: следующий клик мышью берёт цвет пикселя под курсором и делает его затравкой палитры.</summary>
    public void BeginColorPick()
    {
        _picking = true;
        UpdateHooks(true, _hooksKeyboard);
        Island.ShowMessage(Glyphs.ColorPicker, "Кликните по любому цвету на экране", 6);
    }

    private void FinishColorPick(int x, int y)
    {
        uint argb = Native.ScreenPixel(x, y);
        _services.ThemeSettingsChanged(s => s with { Theme = s.Theme with { SeedSource = SeedSource.Custom, CustomSeed = argb } });
        Island.ShowMessage(Glyphs.ColorPicker, "Новый цвет палитры: " + Imagine.Color.ColorUtils.ToHex(argb));
        _applied = null;
        Reconfigure();
    }

    public void Dispose()
    {
        _tick.Stop();
        _fullscreenPoll.Stop();
        _hooks?.Dispose();
        _keyboard.Dispose();
        _tray?.Dispose();
        _messages.Dispose();
        _clock?.Close();
    }
}
