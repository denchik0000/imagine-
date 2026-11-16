using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Imagine.App.Ui;

/// <summary>Небольшой DSL для построения интерфейса кодом (без XAML).</summary>
public static class Ui
{
    public static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    public static readonly FontFamily DisplayFont = new("Segoe UI Variable Display");
    public static readonly FontFamily TextFont = new("Segoe UI Variable Text");

    public static T With<T>(this T element, Action<T> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(element);
        return element;
    }

    public static Windows.UI.Text.FontWeight Weight(int weight) => new() { Weight = (ushort)Math.Clamp(weight, 100, 900) };

    public static TextBlock Text(string text, double size = 14, int weight = 400, Brush? foreground = null, double opacity = 1) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = Weight(weight),
        Opacity = opacity,
        TextWrapping = TextWrapping.WrapWholeWords,
        Foreground = foreground,
    };

    public static TextBlock Title(string text) => Text(text, 28, 600).With(t => t.FontFamily = DisplayFont);

    public static TextBlock Subtitle(string text) => Text(text, 18, 600).With(t => t.Margin = new Thickness(0, 18, 0, 6));

    public static TextBlock Caption(string text) => Text(text, 12, 400, opacity: 0.72);

    public static FontIcon Icon(string glyph, double size = 16, Brush? foreground = null) => new()
    {
        Glyph = glyph,
        FontFamily = IconFont,
        FontSize = size,
        Foreground = foreground,
    };

    public static StackPanel VStack(double spacing, params UIElement[] children)
    {
        var p = new StackPanel { Orientation = Orientation.Vertical, Spacing = spacing };
        foreach (var c in children) p.Children.Add(c);
        return p;
    }

    public static StackPanel HStack(double spacing, params UIElement[] children)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing };
        foreach (var c in children) p.Children.Add(c);
        return p;
    }

    /// <summary>Сетка с колонками вида "Auto,*,120".</summary>
    public static Grid Grid(string columns, params UIElement[] children)
    {
        var g = new Grid { ColumnSpacing = 12 };
        foreach (var c in columns.Split(','))
        {
            var t = c.Trim();
            g.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = t == "Auto" ? GridLength.Auto
                    : t.EndsWith('*') ? new GridLength(t.Length == 1 ? 1 : double.Parse(t[..^1], System.Globalization.CultureInfo.InvariantCulture), GridUnitType.Star)
                    : new GridLength(double.Parse(t, System.Globalization.CultureInfo.InvariantCulture)),
            });
        }
        for (int i = 0; i < children.Length; i++)
        {
            Microsoft.UI.Xaml.Controls.Grid.SetColumn((FrameworkElement)children[i], Math.Min(i, g.ColumnDefinitions.Count - 1));
            g.Children.Add(children[i]);
        }
        return g;
    }

    public static Button Button(string text, Action onClick, string? glyph = null, bool accent = false)
    {
        var b = new Button
        {
            Content = glyph is null ? text : HStack(8, Icon(glyph, 14), Text(text)),
        };
        if (accent) b.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        b.Click += (_, _) => onClick();
        return b;
    }

    public static ToggleSwitch Toggle(bool value, Action<bool> onChanged)
    {
        var t = new ToggleSwitch { IsOn = value, OnContent = "", OffContent = "", MinWidth = 0 };
        t.Toggled += (_, _) => onChanged(t.IsOn);
        return t;
    }

    public static Slider Slider(double min, double max, double value, Action<double> onChanged, double step = 1, double width = 220)
    {
        var s = new Slider { Minimum = min, Maximum = max, Value = value, StepFrequency = step, Width = width, VerticalAlignment = VerticalAlignment.Center };
        s.ValueChanged += (_, e) => onChanged(e.NewValue);
        return s;
    }

    public static ComboBox Combo<T>(IEnumerable<(T Value, string Label)> items, T selected, Action<T> onChanged, double width = 220)
    {
        var list = items.ToList();
        var c = new ComboBox { MinWidth = width };
        foreach (var (_, label) in list) c.Items.Add(label);
        c.SelectedIndex = Math.Max(0, list.FindIndex(i => EqualityComparer<T>.Default.Equals(i.Value, selected)));
        c.SelectionChanged += (_, _) =>
        {
            if (c.SelectedIndex >= 0) onChanged(list[c.SelectedIndex].Value);
        };
        return c;
    }

    public static RadioButtons Radios<T>(IEnumerable<(T Value, string Label)> items, T selected, Action<T> onChanged, int columns = 1)
    {
        var list = items.ToList();
        var r = new RadioButtons { MaxColumns = columns };
        foreach (var (_, label) in list) r.Items.Add(label);
        r.SelectedIndex = Math.Max(0, list.FindIndex(i => EqualityComparer<T>.Default.Equals(i.Value, selected)));
        r.SelectionChanged += (_, _) =>
        {
            if (r.SelectedIndex >= 0) onChanged(list[r.SelectedIndex].Value);
        };
        return r;
    }

    public static TextBox TextBox(string value, Action<string> onCommitted, string? placeholder = null, double width = 220)
    {
        var t = new TextBox { Text = value, PlaceholderText = placeholder ?? "", Width = width };
        t.LostFocus += (_, _) => onCommitted(t.Text);
        t.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter) onCommitted(t.Text);
        };
        return t;
    }

    public static NumberBox Number(double value, double min, double max, Action<double> onChanged, double step = 1)
    {
        var n = new NumberBox { Value = value, Minimum = min, Maximum = max, SmallChange = step, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, MinWidth = 120 };
        n.ValueChanged += (_, e) =>
        {
            if (!double.IsNaN(e.NewValue)) onChanged(e.NewValue);
        };
        return n;
    }

    public static ScrollViewer Scroll(UIElement content, double padding = 32) => new()
    {
        Content = new Border { Padding = new Thickness(padding, 24, padding, 40), Child = content, MaxWidth = 1000, HorizontalAlignment = HorizontalAlignment.Stretch },
        HorizontalScrollMode = ScrollMode.Disabled,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
    };

    public static Brush ThemeBrush(string key) => (Brush)Application.Current.Resources[key];

    public static Border Rounded(UIElement child, double radius, Brush? background = null, Thickness? padding = null) => new()
    {
        Child = child,
        CornerRadius = new CornerRadius(radius),
        Background = background,
        Padding = padding ?? new Thickness(0),
    };
}
