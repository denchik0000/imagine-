using Imagine.Core.Theming;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Imagine.App.Ui;

/// <summary>
/// Contour Glow из ColorOS 17: светящаяся обводка по контуру стеклянной карточки.
/// Градиент медленно вращается (режим «Аврора») или стоит на месте; цвета берутся из палитры.
/// </summary>
public sealed class ContourGlow : Grid
{
    private readonly Border _glow;
    private readonly Border _halo;
    private readonly LinearGradientBrush _brush = new() { MappingMode = BrushMappingMode.RelativeToBoundingBox };
    private readonly LinearGradientBrush _haloBrush = new() { MappingMode = BrushMappingMode.RelativeToBoundingBox };
    private double _angle;
    private bool _animating;
    private GlowMode _mode = GlowMode.Duotone;

    public ContourGlow(UIElement content, double cornerRadius = 24)
    {
        _halo = new Border
        {
            CornerRadius = new CornerRadius(cornerRadius + 3),
            BorderThickness = new Thickness(4),
            BorderBrush = _haloBrush,
            Margin = new Thickness(-3),
            Opacity = 0.25,
            IsHitTestVisible = false,
        };
        _glow = new Border
        {
            CornerRadius = new CornerRadius(cornerRadius),
            BorderThickness = new Thickness(1.5),
            BorderBrush = _brush,
            IsHitTestVisible = false,
        };
        Children.Add(content);
        Children.Add(_halo);
        Children.Add(_glow);
        Refresh();
        Palette.Changed += OnPalette;
        Loaded += (_, _) => SetAnimating(_mode == GlowMode.Aurora);
        Unloaded += (_, _) => SetAnimating(false);
    }

    public double Intensity { get; set; } = 0.7;

    public void Configure(GlowMode mode, double intensity, double thickness, double cornerRadius)
    {
        _mode = mode;
        Intensity = intensity;
        _glow.BorderThickness = new Thickness(thickness);
        _glow.CornerRadius = new CornerRadius(cornerRadius);
        _halo.CornerRadius = new CornerRadius(cornerRadius + 3);
        _halo.BorderThickness = new Thickness(thickness * 3);
        Visibility = Visibility.Visible;
        _glow.Visibility = _halo.Visibility = mode == GlowMode.Off ? Visibility.Collapsed : Visibility.Visible;
        Refresh();
        SetAnimating(mode == GlowMode.Aurora && IsLoaded);
    }

    private void OnPalette(object? sender, ThemePalette e) => DispatcherQueue?.TryEnqueue(Refresh);

    private void Refresh()
    {
        var stops = Palette.GlowStops;
        if (_mode == GlowMode.Accent) stops = [stops[0], stops[0]];
        Fill(_brush, stops, Intensity);
        Fill(_haloBrush, stops, Intensity * 0.6);
        Rotate();
    }

    private static void Fill(LinearGradientBrush brush, IReadOnlyList<uint> stops, double opacity)
    {
        brush.GradientStops.Clear();
        for (int i = 0; i < stops.Count; i++)
            brush.GradientStops.Add(new GradientStop { Color = Palette.ToColor(stops[i], opacity), Offset = stops.Count == 1 ? 0 : i / (double)(stops.Count - 1) });
    }

    private void Rotate()
    {
        double r = _angle * Math.PI / 180;
        var start = new Point(0.5 - Math.Cos(r) / 2, 0.5 - Math.Sin(r) / 2);
        var end = new Point(0.5 + Math.Cos(r) / 2, 0.5 + Math.Sin(r) / 2);
        _brush.StartPoint = _haloBrush.StartPoint = start;
        _brush.EndPoint = _haloBrush.EndPoint = end;
    }

    private void SetAnimating(bool on)
    {
        if (on == _animating) return;
        _animating = on;
        if (on) CompositionTarget.Rendering += OnFrame;
        else CompositionTarget.Rendering -= OnFrame;
    }

    private long _last;

    private void OnFrame(object? sender, object e)
    {
        long now = Environment.TickCount64;
        // ~12 градусов в секунду: спокойное «дыхание», не отвлекает.
        if (_last != 0) _angle = (_angle + (now - _last) * 0.012) % 360;
        _last = now;
        Rotate();
    }
}

/// <summary>Полоса тонов палитры (как в «Обоях и стиле» Android).</summary>
public static class Swatches
{
    public static Border Dot(uint argb, double size = 28, bool selected = false) => new()
    {
        Width = size,
        Height = size,
        CornerRadius = new CornerRadius(size / 2),
        Background = Palette.Brush(argb),
        BorderThickness = new Thickness(selected ? 3 : 1),
        BorderBrush = selected ? Palette.Get(Imagine.Color.ColorRole.OnSurface) : new SolidColorBrush(Microsoft.UI.Colors.Transparent),
    };

    public static Grid ToneStrip(Imagine.Color.TonalPalette palette, string name)
    {
        double[] tones = [0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 95, 99, 100];
        var g = new Grid { Height = 36, CornerRadius = new CornerRadius(10) };
        for (int i = 0; i < tones.Length; i++)
        {
            g.ColumnDefinitions.Add(new ColumnDefinition());
            var c = new Border { Background = Palette.Brush(palette.Tone(tones[i])) };
            ToolTipService.SetToolTip(c, $"{name} {tones[i]}: {Imagine.Color.ColorUtils.ToHex(palette.Tone(tones[i]))}");
            SetColumn(c, i);
            g.Children.Add(c);
        }
        return g;
    }

    private static void SetColumn(FrameworkElement e, int c) => Grid.SetColumn(e, c);

    /// <summary>Мини-превью схемы: круг, поделённый на основной, вторичный и третичный, как в Android.</summary>
    public static Grid SchemeBadge(Imagine.Color.DynamicScheme scheme, double size = 56, bool selected = false)
    {
        var g = new Grid { Width = size, Height = size, CornerRadius = new CornerRadius(size / 2) };
        g.RowDefinitions.Add(new RowDefinition());
        g.RowDefinitions.Add(new RowDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition());
        var top = new Border { Background = Palette.Brush(scheme.Get(Imagine.Color.ColorRole.PrimaryContainer)) };
        Grid.SetColumnSpan(top, 2);
        var bl = new Border { Background = Palette.Brush(scheme.Get(Imagine.Color.ColorRole.SecondaryContainer)) };
        Grid.SetRow(bl, 1);
        var br = new Border { Background = Palette.Brush(scheme.Get(Imagine.Color.ColorRole.TertiaryContainer)) };
        Grid.SetRow(br, 1);
        Grid.SetColumn(br, 1);
        var center = new Border
        {
            Width = size * 0.38,
            Height = size * 0.38,
            CornerRadius = new CornerRadius(size * 0.19),
            Background = Palette.Brush(scheme.Get(Imagine.Color.ColorRole.Primary)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetRowSpan(center, 2);
        Grid.SetColumnSpan(center, 2);
        g.Children.Add(top);
        g.Children.Add(bl);
        g.Children.Add(br);
        g.Children.Add(center);
        if (selected)
        {
            var ring = new Border { BorderThickness = new Thickness(3), CornerRadius = new CornerRadius(size / 2), BorderBrush = Palette.Get(Imagine.Color.ColorRole.OnSurface) };
            Grid.SetRowSpan(ring, 2);
            Grid.SetColumnSpan(ring, 2);
            g.Children.Add(ring);
        }
        return g;
    }
}
