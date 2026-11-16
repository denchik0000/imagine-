using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Imagine.App.Ui;
using Imagine.Color;
using Imagine.Core.Motion;
using Imagine.Core.Services;
using Imagine.Platform.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace Imagine.App.Shell;

/// <summary>
/// «Островок» у верхнего края: на пару секунд показывает, что играет, сменился стиль палитры,
/// включился фокус и т. п. Не забирает фокус; нажатие открывает быстрые настройки.
/// </summary>
public sealed class IslandWindow : OverlayWindow
{
    private const int HeightDip = 52;
    private readonly AppServices _services;
    private readonly GlassBackdrop _glass = new();
    private readonly Grid _root = new() { Padding = new Thickness(8, 6, 16, 6), ColumnSpacing = 10 };
    private readonly Spring _spring = new(0, SpringSpec.Bouncy);
    private readonly DispatcherTimerLite _hideTimer;
    private int _targetWidth;
    private long _lastFrame;
    private bool _animating;
    private string? _lastTrack;

    public IslandWindow(AppServices services) : base(topmost: true, noActivate: true)
    {
        _services = services;
        Title = "Imagine — островок";
        SystemBackdrop = _glass;
        WindowStyler.SetCorners(Hwnd, true);
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _root.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        _root.Tapped += (_, _) => App.Current.Shell.Open(Core.Shades.ShadeKind.QuickSettings, Core.Shades.ShadeTrigger.Tray);
        Content = _root;
        _hideTimer = new DispatcherTimerLite(DispatcherQueue, TimeSpan.FromSeconds(3), Collapse);
        Palette.Changed += (_, t) => DispatcherQueue.TryEnqueue(() =>
        {
            _glass.SetTint(t.Glass.Tint, Math.Min(0.85, t.Glass.TintOpacity + 0.2), t.Glass.LuminosityOpacity);
            _root.RequestedTheme = t.IsDark ? ElementTheme.Dark : ElementTheme.Light;
        });
        services.Media.Changed += (_, m) => DispatcherQueue.TryEnqueue(() => OnMedia(m));
    }

    private bool Enabled => _services.Current.Shades.Island;

    public void ShowMessage(string glyph, string text, double seconds = 2.5)
    {
        if (!Enabled) return;
        _root.Children.Clear();
        var icon = new Border
        {
            Width = 36, Height = 36, CornerRadius = new CornerRadius(18),
            Background = Palette.Get(ColorRole.PrimaryContainer),
            Child = Ui.Ui.Icon(glyph, 15, Palette.Get(ColorRole.OnPrimaryContainer)).With(i => { i.HorizontalAlignment = HorizontalAlignment.Center; i.VerticalAlignment = VerticalAlignment.Center; }),
        };
        var label = Ui.Ui.Text(text, 13, 600, Palette.Get(ColorRole.OnSurface)).With(t => { t.VerticalAlignment = VerticalAlignment.Center; t.TextTrimming = TextTrimming.CharacterEllipsis; t.TextWrapping = TextWrapping.NoWrap; });
        Grid.SetColumn(label, 1);
        _root.Children.Add(icon);
        _root.Children.Add(label);
        Expand(Math.Clamp(80 + text.Length * 7.5, 200, 420), seconds);
    }

    private async void OnMedia(MediaState? m)
    {
        if (!Enabled || m is null || !m.IsPlaying) return;
        string key = m.Title + "|" + m.Artist;
        if (key == _lastTrack) return;
        _lastTrack = key;
        _root.Children.Clear();
        var art = new Border { Width = 38, Height = 38, CornerRadius = new CornerRadius(10), Background = Palette.Get(ColorRole.TertiaryContainer) };
        if (m.Thumbnail is { Length: > 0 } bytes)
        {
            try
            {
                var bmp = new BitmapImage();
                using var stream = new InMemoryRandomAccessStream();
                await stream.WriteAsync(bytes.AsBuffer());
                stream.Seek(0);
                await bmp.SetSourceAsync(stream);
                art.Child = new Image { Source = bmp, Stretch = Stretch.UniformToFill };
            }
            catch (COMException)
            {
            }
        }
        var texts = Ui.Ui.VStack(0,
            Ui.Ui.Text(m.Title, 13, 600, Palette.Get(ColorRole.OnSurface)).With(t => { t.TextTrimming = TextTrimming.CharacterEllipsis; t.TextWrapping = TextWrapping.NoWrap; }),
            Ui.Ui.Text(m.Artist, 11, 400, Palette.Get(ColorRole.OnSurfaceVariant)).With(t => { t.TextTrimming = TextTrimming.CharacterEllipsis; t.TextWrapping = TextWrapping.NoWrap; }));
        texts.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(texts, 1);
        var eq = Ui.Ui.Icon(Glyphs.Music, 14, Palette.Get(ColorRole.Primary)).With(i => i.VerticalAlignment = VerticalAlignment.Center);
        Grid.SetColumn(eq, 2);
        _root.Children.Add(art);
        _root.Children.Add(texts);
        _root.Children.Add(eq);
        Expand(340, 4);
    }

    private void Expand(double widthDip, double seconds)
    {
        var area = Monitors.Primary();
        if (area is null) return;
        double scale = Scale;
        _targetWidth = (int)(widthDip * scale);
        int h = (int)(HeightDip * scale);
        int x = area.Bounds.Left + (area.Bounds.Width - _targetWidth) / 2;
        Place(x, area.Bounds.Top + (int)(8 * scale), (int)(_targetWidth * Math.Max(0.3, _spring.Value)), h);
        ShowWithoutFocus();
        _spring.AnimateTo(1);
        Start();
        _hideTimer.Stop();
        _hideTimer.Interval = TimeSpan.FromSeconds(seconds);
        _hideTimer.Start();
    }

    private void Collapse()
    {
        _hideTimer.Stop();
        _spring.AnimateTo(0);
        Start();
    }

    private void Start()
    {
        if (_animating) return;
        _animating = true;
        _lastFrame = Environment.TickCount64;
        CompositionTarget.Rendering += OnFrame;
    }

    private void OnFrame(object? sender, object e)
    {
        long now = Environment.TickCount64;
        bool moving = _spring.Step(Math.Min(0.05, (now - _lastFrame) / 1000.0));
        _lastFrame = now;
        var area = Monitors.Primary();
        if (area is not null)
        {
            double p = Math.Max(0, _spring.Value);
            int w = (int)(_targetWidth * (0.3 + 0.7 * p));
            Place(area.Bounds.Left + (area.Bounds.Width - w) / 2, area.Bounds.Top + (int)(8 * Scale), w, (int)(HeightDip * Scale));
            _root.Opacity = Math.Clamp(p * 1.5 - 0.3, 0, 1);
        }
        if (!moving)
        {
            CompositionTarget.Rendering -= OnFrame;
            _animating = false;
            if (_spring.Value <= 0.01) Hide();
        }
    }
}
