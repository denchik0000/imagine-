using System.Numerics;
using Imagine.Core.Clock;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Imagine.App.Ui;

/// <summary>
/// Часы Imagine: четыре раскладки (столбик, строка, журнальная, стрелочные), цвет из палитры
/// и мягкое свечение цифр. Один и тот же элемент рисует часы на рабочем столе и превью в настройках.
/// </summary>
public sealed class ClockView : Grid
{
    private readonly Grid _host = new();
    private readonly Grid _shadowHost = new();
    private ClockStyle _style = new();
    private double _fontSize = 96;
    private Brush _foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
    private Brush _secondary = new SolidColorBrush(Microsoft.UI.Colors.White);
    private uint _glowColor = 0xFFFFFFFF;
    private string _lastKey = "";
    private TextBlock? _main;
    private TextBlock? _date;
    private Line? _hourHand, _minuteHand, _secondHand;

    public ClockView()
    {
        Children.Add(_shadowHost);
        Children.Add(_host);
        IsHitTestVisible = false;
        SizeChanged += (_, _) => UpdateGlow();
    }

    public void Configure(ClockStyle style, double fontSize, Brush foreground, Brush secondary, uint glowColor)
    {
        _style = style;
        _fontSize = Math.Max(8, fontSize);
        _foreground = foreground;
        _secondary = secondary;
        _glowColor = glowColor;
        _lastKey = "";
        Build();
        Tick(DateTime.Now);
    }

    private void Build()
    {
        _host.Children.Clear();
        _host.RowDefinitions.Clear();
        _host.ColumnDefinitions.Clear();
        _main = null;
        _date = null;
        _hourHand = _minuteHand = _secondHand = null;
        var align = _style.Alignment switch
        {
            ClockAlignment.Center => HorizontalAlignment.Center,
            ClockAlignment.Right => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Left,
        };
        _host.HorizontalAlignment = align;

        if (_style.Layout == ClockLayout.Analog)
        {
            BuildAnalog();
            return;
        }

        var panel = new StackPanel { HorizontalAlignment = align, Spacing = 0 };
        _main = Digits(_fontSize);
        _main.TextAlignment = _style.Alignment switch { ClockAlignment.Center => TextAlignment.Center, ClockAlignment.Right => TextAlignment.Right, _ => TextAlignment.Left };
        if (_style.Layout == ClockLayout.Stacked)
        {
            _main.LineHeight = _fontSize * 0.9;
            _main.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        }
        var date = Ui.Text("", Math.Max(11, _fontSize * (_style.Layout == ClockLayout.Editorial ? 0.16 : 0.2)), 500, _secondary);
        _date = date;
        date.HorizontalAlignment = align;
        date.Visibility = _style.ShowDate ? Visibility.Visible : Visibility.Collapsed;

        if (_style.Layout == ClockLayout.Editorial)
        {
            // Журнальная вёрстка: дата вертикально слева, тонкие цифры справа.
            date.FontWeight = Ui.Weight(600);
            date.CharacterSpacing = 200;
            date.RenderTransform = new RotateTransform { Angle = -90 };
            date.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            var row = new Grid { ColumnSpacing = _fontSize * 0.08 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_fontSize * 0.3) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var dateBox = new Canvas { Width = _fontSize * 0.3 };
            date.TextWrapping = TextWrapping.NoWrap;
            date.Loaded += (_, _) =>
            {
                Canvas.SetLeft(date, (_fontSize * 0.3 - date.ActualWidth) / 2);
                Canvas.SetTop(date, (_main.ActualHeight - date.ActualHeight) / 2);
            };
            dateBox.Children.Add(date);
            row.Children.Add(dateBox);
            Grid.SetColumn(_main, 1);
            row.Children.Add(_main);
            panel.Children.Add(row);
        }
        else
        {
            panel.Children.Add(_main);
            date.Margin = new Thickness(_fontSize * 0.04, _fontSize * 0.04, 0, 0);
            panel.Children.Add(date);
        }
        _host.Children.Add(panel);
    }

    private TextBlock Digits(double size) => new()
    {
        FontSize = size,
        FontFamily = new FontFamily(_style.FontFamily),
        FontWeight = Ui.Weight(_style.FontWeight),
        Foreground = _foreground,
        TextWrapping = TextWrapping.NoWrap,
        CharacterSpacing = _style.Layout == ClockLayout.Stacked ? -40 : -20,
    };

    private void BuildAnalog()
    {
        double d = _fontSize * 1.6;
        var canvas = new Canvas { Width = d, Height = d };
        var face = new Ellipse { Width = d, Height = d, Stroke = _secondary, StrokeThickness = Math.Max(1, d * 0.01), Opacity = 0.5 };
        canvas.Children.Add(face);
        for (int i = 0; i < 12; i++)
        {
            double a = i * Math.PI / 6;
            double r1 = d / 2 * (i % 3 == 0 ? 0.78 : 0.86), r2 = d / 2 * 0.93;
            canvas.Children.Add(new Line
            {
                X1 = d / 2 + Math.Sin(a) * r1, Y1 = d / 2 - Math.Cos(a) * r1,
                X2 = d / 2 + Math.Sin(a) * r2, Y2 = d / 2 - Math.Cos(a) * r2,
                Stroke = i % 3 == 0 ? _foreground : _secondary,
                StrokeThickness = d * (i % 3 == 0 ? 0.022 : 0.012),
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            });
        }
        Line Hand(double length, double thickness, Brush brush) => new()
        {
            X1 = d / 2, Y1 = d / 2, X2 = d / 2, Y2 = d / 2 - length,
            Stroke = brush, StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            RenderTransform = new RotateTransform { CenterX = d / 2, CenterY = d / 2 },
        };
        _hourHand = Hand(d * 0.24, d * 0.045, _foreground);
        _minuteHand = Hand(d * 0.36, d * 0.03, _foreground);
        canvas.Children.Add(_hourHand);
        canvas.Children.Add(_minuteHand);
        if (_style.ShowSeconds)
        {
            _secondHand = Hand(d * 0.4, d * 0.01, _secondary);
            canvas.Children.Add(_secondHand);
        }
        canvas.Children.Add(new Ellipse { Width = d * 0.06, Height = d * 0.06, Fill = _foreground, Margin = new Thickness(d * 0.47, d * 0.47, 0, 0) });
        _host.Children.Add(canvas);
    }

    public void Tick(DateTime now)
    {
        if (_style.Layout == ClockLayout.Analog)
        {
            var (h, m, s) = ClockFormatter.HandAngles(now);
            ((RotateTransform)_hourHand!.RenderTransform).Angle = h;
            ((RotateTransform)_minuteHand!.RenderTransform).Angle = m;
            if (_secondHand is not null) ((RotateTransform)_secondHand.RenderTransform).Angle = s;
            return;
        }
        var (hh, mm, ss, ampm) = ClockFormatter.Digits(now, _style);
        string key = hh + mm + ss + ampm + now.Day;
        if (key == _lastKey || _main is null) return;
        _lastKey = key;
        _main.Text = _style.Layout switch
        {
            ClockLayout.Stacked => hh + "\n" + mm,
            _ => $"{hh}:{mm}" + (ss is null ? "" : ":" + ss) + (ampm is null ? "" : " " + ampm),
        };
        if (_date is not null) _date.Text = ClockFormatter.LongDate(now);
        UpdateGlow();
    }

    /// <summary>Свечение цифр: тень композиции по альфа-маске текста.</summary>
    private void UpdateGlow()
    {
        if (!_style.Glow || _main is null || _main.ActualWidth <= 0)
        {
            ElementCompositionPreview.SetElementChildVisual(_shadowHost, null);
            return;
        }
        try
        {
            var compositor = ElementCompositionPreview.GetElementVisual(_shadowHost).Compositor;
            var shadow = compositor.CreateDropShadow();
            shadow.BlurRadius = (float)(_fontSize * 0.18);
            shadow.Color = Palette.ToColor(_glowColor, 0.55);
            shadow.Mask = _main.GetAlphaMask();
            var sprite = compositor.CreateSpriteVisual();
            sprite.Size = new Vector2((float)_main.ActualWidth, (float)_main.ActualHeight);
            sprite.Shadow = shadow;
            var offset = _main.TransformToVisual(_shadowHost).TransformPoint(new Windows.Foundation.Point(0, 0));
            sprite.Offset = new Vector3((float)offset.X, (float)offset.Y, 0);
            ElementCompositionPreview.SetElementChildVisual(_shadowHost, sprite);
        }
        catch (ArgumentException)
        {
        }
    }
}
