using Imagine.App.Ui;
using Imagine.Color;
using Imagine.Core.Motion;
using Imagine.Core.Shades;
using Imagine.Core.Theming;
using Imagine.Platform.Windowing;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Imagine.App.Shell;

/// <summary>
/// Шторка в стиле Android: выезжает от верхнего края на пружине, на матовом стекле с тинтом палитры
/// и светящимся контуром. Закрывается кликом мимо, Esc, жестом вверх по «ручке» или той же горячей клавишей.
/// </summary>
public abstract class ShadeWindow : OverlayWindow
{
    private const double Margin = 10;
    private readonly GlassBackdrop _glass = new();
    private readonly Spring _spring = new(0, SpringSpec.Fluid);
    private readonly Grid _panel;
    private readonly ContourGlow _glow;
    private bool _dragging;
    private DisplayArea? _area;
    private int _fullHeight;
    private int _x;
    private long _lastFrame;
    private bool _animating;
    private bool _closing;

    protected ShadeWindow(AppServices services, ShadeKind kind) : base(topmost: true, noActivate: false)
    {
        Services = services;
        Kind = kind;
        SystemBackdrop = _glass;
        WindowStyler.SetCorners(Hwnd, true);

        _panel = new Grid { VerticalAlignment = VerticalAlignment.Bottom, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
        _panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var handle = new Border
        {
            Height = 22,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            Child = new Border { Width = 44, Height = 4, CornerRadius = new CornerRadius(2), Background = Palette.Get(ColorRole.OnSurfaceVariant, 0.5), VerticalAlignment = VerticalAlignment.Center },
        };
        handle.PointerPressed += OnHandlePressed;
        handle.PointerMoved += OnHandleMoved;
        handle.PointerReleased += OnHandleReleased;
        Grid.SetRow(handle, 1);
        _panel.Children.Add(handle);

        _glow = new ContourGlow(_panel, 24);
        var root = new Grid { RequestedTheme = ElementTheme.Default };
        root.Children.Add(_glow);
        root.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Escape) Close(animated: true);
        };
        Content = root;

        Activated += (_, e) =>
        {
            if (e.WindowActivationState == WindowActivationState.Deactivated && IsOpen && !_dragging) Close(animated: true);
        };
        Palette.Changed += (_, t) => DispatcherQueue.TryEnqueue(() => ApplyTheme(t));
        if (Palette.Current is { } current) ApplyTheme(current);
    }

    protected AppServices Services { get; }

    public ShadeKind Kind { get; }

    public bool IsOpen { get; private set; }

    public event EventHandler? Opened;
    public event EventHandler? ClosedShade;

    /// <summary>Содержимое шторки (заполняет наследник).</summary>
    protected void SetBody(UIElement body)
    {
        if (body is FrameworkElement fe) Grid.SetRow(fe, 0);
        _panel.Children.Insert(0, body);
    }

    /// <summary>Обновить данные перед показом.</summary>
    protected abstract Task RefreshAsync();

    private void ApplyTheme(ThemePalette theme)
    {
        var s = Services.Current.Glass;
        var glass = theme.Glass;
        _glass.SetTint(glass.Tint, glass.TintOpacity, glass.LuminosityOpacity);
        _glow.Configure(s.GlowMode, glass.GlowIntensity, glass.GlowThickness, glass.CornerRadius);
        if (Content is FrameworkElement fe) fe.RequestedTheme = theme.IsDark ? ElementTheme.Dark : ElementTheme.Light;
        WindowStyler.SetDarkFrame(Hwnd, theme.IsDark);
    }

    public async void Open(ShadeRequest request)
    {
        if (IsOpen && !_closing) return;
        _closing = false;
        var (cx, cy) = Monitors.CursorPosition();
        _area = Monitors.FromPoint(request.Trigger is ShadeTrigger.Hotkey or ShadeTrigger.Tray ? cx : (int)request.AnchorX, cy) ?? Monitors.Primary();
        if (_area is null) return;

        double scale = Scale;
        var work = _area.WorkArea;
        int width = (int)Math.Round(Services.Current.Shades.Width * scale);
        int margin = (int)Math.Round(Margin * scale);
        _fullHeight = work.Height - 2 * margin;
        bool left = Kind == ShadeKind.Notifications ^ Services.Current.Shades.Edge.SwapSides;
        _x = left ? work.Left + margin : work.Right - margin - width;
        _panel.Height = _fullHeight / scale;
        
        IsOpen = true;
        try { await RefreshAsync(); }
        catch (Exception ex) { Log.Error("Обновление шторки", ex); }

        Place(_x, work.Top + margin, width, 1);
        Activate();
        WindowStyler.BringToFront(Hwnd, true);
        _spring.AnimateTo(1);
        StartAnimation();
        Opened?.Invoke(this, EventArgs.Empty);
    }

    public void Toggle(ShadeRequest request)
    {
        if (IsOpen && !_closing) Close(animated: true);
        else Open(request);
    }

    public void Close(bool animated)
    {
        if (!IsOpen || _closing) return;
        _closing = true;
        if (!animated)
        {
            _spring.SnapTo(0);
            FinishClose();
            return;
        }
        _spring.AnimateTo(0);
        StartAnimation();
    }

    private void FinishClose()
    {
        Hide();
        IsOpen = false;
        _closing = false;
        ClosedShade?.Invoke(this, EventArgs.Empty);
    }

    private void StartAnimation()
    {
        if (_animating) return;
        _animating = true;
        _lastFrame = Environment.TickCount64;
        CompositionTarget.Rendering += OnFrame;
    }

    private void OnFrame(object? sender, object e)
    {
        long now = Environment.TickCount64;
        double dt = Math.Min(0.05, (now - _lastFrame) / 1000.0);
        _lastFrame = now;
        bool moving = _spring.Step(dt);
        Render(_spring.Value);
        if (!moving)
        {
            CompositionTarget.Rendering -= OnFrame;
            _animating = false;
            if (_closing && _spring.Value <= 0.001) FinishClose();
        }
    }

    /// <summary>Высота окна растёт вместе с прогрессом; содержимое прижато к низу, поэтому «съезжает» сверху.</summary>
    private void Render(double progress)
    {
        if (_area is null) return;
        double p = Math.Max(0, progress);
        int margin = (int)Math.Round(Margin * Scale);
        int h = (int)Math.Round(_fullHeight * Math.Min(1.04, p));
        Place(_x, _area.WorkArea.Top + margin, AppWindow.Size.Width, Math.Max(1, h));
        _panel.Opacity = Math.Clamp(p * 1.6, 0, 1);
    }

    private double _dragStartY;
    private double _dragStartProgress;
    private (long Ts, double Y) _prevSample, _lastSample;

    private void OnHandlePressed(object sender, PointerRoutedEventArgs e)
    {
        ((UIElement)sender).CapturePointer(e.Pointer);
        var (_, y) = Monitors.CursorPosition();
        _dragging = true;
        _dragStartY = y;
        _dragStartProgress = _spring.Value;
        _prevSample = _lastSample = (Environment.TickCount64, y);
        e.Handled = true;
    }

    private void OnHandleMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging) return;
        var (_, y) = Monitors.CursorPosition();
        long now = Environment.TickCount64;
        if (now - _lastSample.Ts > 10) { _prevSample = _lastSample; _lastSample = (now, y); }
        double p = _dragStartProgress + (y - _dragStartY) / Math.Max(1, _fullHeight);
        // Вниз за край — резиновое сопротивление.
        if (p > 1) p = 1 + Easing.RubberBand(p - 1, 0.25);
        _spring.SnapTo(Math.Max(0, p));
        Render(_spring.Value);
    }

    private void OnHandleReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        ((UIElement)sender).ReleasePointerCapture(e.Pointer);
        double dt = Math.Max(1, _lastSample.Ts - _prevSample.Ts) / 1000.0;
        double v = (_lastSample.Y - _prevSample.Y) / dt / Math.Max(1, _fullHeight); // доли высоты в секунду
        bool close = v < -1.2 || (v < 1.2 && _spring.Value < 0.6);
        if (close)
        {
            _closing = true;
            _spring.AnimateTo(0, v);
        }
        else _spring.AnimateTo(1, v);
        StartAnimation();
    }

    /// <summary>Общие элементы: заголовок раздела.</summary>
    protected static TextBlock SectionTitle(string text) => Ui.Ui.Text(text, 13, 600, Palette.Get(ColorRole.OnSurfaceVariant)).With(t => t.Margin = new Thickness(4, 10, 0, 2));

    /// <summary>Скруглённая плашка на стекле.</summary>
    protected static Border Card(UIElement child, double radius = 20, double padding = 14) => new()
    {
        Child = child,
        CornerRadius = new CornerRadius(radius),
        Padding = new Thickness(padding),
        Background = Palette.Get(ColorRole.SurfaceContainerHigh, 0.55),
        BorderBrush = Palette.Get(ColorRole.OutlineVariant, 0.35),
        BorderThickness = new Thickness(1),
    };
}
