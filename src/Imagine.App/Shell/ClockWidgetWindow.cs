using Imagine.App.Ui;
using Imagine.Color;
using Imagine.Core.Clock;
using Imagine.Core.Motion;
using Imagine.Platform.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Imagine.App.Shell;

/// <summary>
/// Часы на рабочем столе, встроенные под значки. После разблокировки они «переезжают» из центра экрана
/// (где были часы экрана блокировки) на своё место — как в ColorOS 17.
/// </summary>
public sealed class ClockWidgetWindow : OverlayWindow
{
    private readonly AppServices _services;
    private readonly ClockView _clock = new();
    private readonly Canvas _canvas = new();
    private readonly DispatcherTimerLite _timer;
    private readonly Spring _fly = new(1, SpringSpec.Gentle);
    private readonly TransparentBackdrop _backdrop = new();
    private bool _attached;
    private ClockFrame _frame;
    private ClockFrame _from;
    private DisplayArea? _area;
    private long _lastFrame;
    private bool _flying;

    public ClockWidgetWindow(AppServices services) : base(topmost: false, noActivate: true)
    {
        _services = services;
        Title = "Imagine — часы";
        SystemBackdrop = _backdrop;
        _canvas.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        _canvas.Children.Add(_clock);
        Content = _canvas;
        _timer = new DispatcherTimerLite(DispatcherQueue, TimeSpan.FromSeconds(1), () => _clock.Tick(DateTime.Now));
        // Клик по часам или Alt+Tab мог поднять окно — возвращаем его вниз.
        Activated += (_, _) => WindowStyler.SendToBottom(Hwnd);
        Palette.Changed += (_, _) => DispatcherQueue.Post(Restyle);
        services.Settings.Changed += (_, settings) =>
        {
            // Перестраиваем часы, только если поменялись именно их настройки.
            if (Equals(settings.Clock, _lastClock)) return;
            DispatcherQueue.Post(Restyle);
        };
    }

    public void ShowClock()
    {
        if (!_services.Current.Clock.DesktopClock)
        {
            HideClock();
            return;
        }
        Restyle();
        AppWindow.Show(false);
        // Не встраиваемся в окно Проводника (WorkerW): родитель из чужого процесса связывает
        // очереди ввода, и любая заминка одного процесса подвешивает рабочий стол другого.
        // Вместо этого держим часы обычным окном в самом низу стопки.
        WindowStyler.SendToBottom(Hwnd);
        Layout();
        _timer.Start();
    }

    public void HideClock()
    {
        _timer.Stop();
        Hide();
    }

    private Core.Settings.ClockSettings? _lastClock;

    private void Restyle()
    {
        var s = _services.Current.Clock;
        _lastClock = s;
        if (!s.DesktopClock)
        {
            if (IsVisible) HideClock();
            return;
        }
        _area = Monitors.Primary();
        if (_area is null) return;
        _frame = FluidClockLayout.Compute(s.Style, _area.Bounds.Width, _area.Bounds.Height);
        var theme = Palette.Current;
        bool topLight = _services.Engine.CurrentAnalysis?.TopIsLight ?? false;
        var role = s.Style.ColorRole ?? (topLight ? ColorRole.OnPrimaryFixedVariant : ColorRole.PrimaryFixed);
        var secondaryRole = topLight ? ColorRole.OnSecondaryFixedVariant : ColorRole.SecondaryFixed;
        uint glow = theme?[ColorRole.Primary] ?? 0xFFD55E5D;
        double scale = Scale;
        _clock.Configure(s.Style, _frame.FontSize / scale, Palette.Get(role), Palette.Get(secondaryRole, 0.9), glow);
        if (!_flying) Layout();
    }

    /// <summary>Окно чуть больше рамки часов: прозрачные поля не мешают, а свечению есть куда растечься.</summary>
    private void Layout()
    {
        if (_area is null) return;
        double scale = Scale;
        double pad = _frame.FontSize * 0.35;
        int w = (int)(_frame.Width * 1.6 + pad * 2), h = (int)(_frame.Height * 1.35 + pad * 2);
        int x = (int)(_area.Bounds.Left + _frame.X - pad), y = (int)(_area.Bounds.Top + _frame.Y - pad);
        if (_style.Alignment == ClockAlignment.Right) x -= (int)(_frame.Width * 0.6);
        MoveTo(x, y, w, h);
        Canvas.SetLeft(_clock, pad / scale);
        Canvas.SetTop(_clock, pad / scale);
        _clock.RenderTransform = null;
    }

    private ClockStyle _style => _services.Current.Clock.Style;

    private void MoveTo(int x, int y, int w, int h)
    {
        if (_attached)
        {
            // Дочернее окно WorkerW: координаты считаются от его левого верхнего угла.
            var parent = Native.GetParent(Hwnd);
            if (parent != IntPtr.Zero && Native.GetWindowRect(parent, out var r)) { x -= r.Left; y -= r.Top; }
            WindowStyler.MoveResize(Hwnd, x, y, w, h);
        }
        else Place(x, y, w, h);
    }

    /// <summary>Анимация после разблокировки: из крупных часов по центру — в свою рамку.</summary>
    public void FlyIn()
    {
        if (!_services.Current.Clock.FlyInAfterUnlock || !IsVisible || _area is null) return;
        double sw = _area.Bounds.Width, sh = _area.Bounds.Height;
        _from = FluidClockLayout.Compute(_style with { Placement = ClockPlacement.TopCenter, RelativeSize = Math.Max(_style.RelativeSize * 1.5, 0.2) }, sw, sh) with { Y = sh * 0.12 };
        MoveTo(_area.Bounds.Left, _area.Bounds.Top, (int)sw, (int)sh);
        Canvas.SetLeft(_clock, 0);
        Canvas.SetTop(_clock, 0);
        _flying = true;
        _fly.SnapTo(0);
        _fly.AnimateTo(1);
        _lastFrame = Environment.TickCount64;
        RenderFly();
        CompositionTarget.Rendering += OnFrame;
    }

    private void OnFrame(object? sender, object e)
    {
        try
        {
            OnFrameCore();
        }
        catch (Exception ex)
        {
            // Кадр анимации не должен ронять приложение: останавливаем анимацию и пишем в журнал.
            CompositionTarget.Rendering -= OnFrame;
            Log.Error("Кадр анимации", ex);
        }
    }

    private void OnFrameCore()
    {
        long now = Environment.TickCount64;
        bool moving = _fly.Step(Math.Min(0.05, (now - _lastFrame) / 1000.0));
        _lastFrame = now;
        RenderFly();
        if (!moving)
        {
            CompositionTarget.Rendering -= OnFrame;
            _flying = false;
            Layout();
        }
    }

    private void RenderFly()
    {
        double scale = Scale;
        var f = FluidClockLayout.Interpolate(_from, _frame, _fly.Value);
        double k = f.FontSize / _frame.FontSize;
        _clock.RenderTransform = new CompositeTransform { TranslateX = f.X / scale, TranslateY = f.Y / scale, ScaleX = k, ScaleY = k };
    }
}
