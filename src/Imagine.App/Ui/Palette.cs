using Imagine.Color;
using Imagine.Core.Theming;
using Microsoft.UI.Xaml.Media;
using WinColor = Windows.UI.Color;

namespace Imagine.App.Ui;

/// <summary>
/// Живые кисти палитры. Каждая роль — один экземпляр SolidColorBrush, который перекрашивается на месте,
/// поэтому весь интерфейс шторок и часов меняет цвет вместе с обоями без перестройки.
/// </summary>
public static class Palette
{
    private static readonly Dictionary<ColorRole, SolidColorBrush> Brushes = new();
    private static readonly Dictionary<(ColorRole, byte), SolidColorBrush> Alpha = new();

    public static ThemePalette? Current { get; private set; }

    public static event EventHandler<ThemePalette>? Changed;

    public static WinColor ToColor(uint argb) => Microsoft.UI.ColorHelper.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

    public static WinColor ToColor(uint argb, double opacity) => Microsoft.UI.ColorHelper.FromArgb((byte)Math.Round(255 * Math.Clamp(opacity, 0, 1)), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

    /// <summary>Цвет до появления первой темы — пудрово-розовый, как в исходной идее.</summary>
    private static uint Fallback(ColorRole role) => role switch
    {
        ColorRole.Primary => 0xFF904A49,
        ColorRole.OnPrimary => 0xFFFFFFFF,
        ColorRole.PrimaryContainer => 0xFFFFDAD8,
        ColorRole.OnPrimaryContainer => 0xFF3B080B,
        ColorRole.OnSurface or ColorRole.OnBackground => 0xFF231919,
        ColorRole.OnSurfaceVariant => 0xFF534342,
        ColorRole.Outline => 0xFF857372,
        _ => 0xFFFFF8F7,
    };

    private static uint Resolve(ColorRole role) => Current is { } t && t.Roles.TryGetValue(role, out var c) ? c : Fallback(role);

    public static SolidColorBrush Get(ColorRole role)
    {
        if (!Brushes.TryGetValue(role, out var b))
            Brushes[role] = b = new SolidColorBrush(ToColor(Resolve(role)));
        return b;
    }

    /// <summary>Полупрозрачная кисть роли (для стеклянных подложек).</summary>
    public static SolidColorBrush Get(ColorRole role, double opacity)
    {
        var key = (role, (byte)Math.Round(255 * Math.Clamp(opacity, 0, 1)));
        if (!Alpha.TryGetValue(key, out var b))
            Alpha[key] = b = new SolidColorBrush(ToColor(Resolve(role), opacity));
        return b;
    }

    public static SolidColorBrush Brush(uint argb) => new(ToColor(argb));

    public static void Apply(ThemePalette theme)
    {
        Current = theme;
        foreach (var (role, brush) in Brushes) brush.Color = ToColor(Resolve(role));
        foreach (var ((role, a), brush) in Alpha) brush.Color = ToColor(Resolve(role), a / 255.0);
        Changed?.Invoke(null, theme);
    }

    /// <summary>Набор цветов свечения (контурный градиент).</summary>
    public static IReadOnlyList<uint> GlowStops => Current?.Glass.GlowStops ?? [0xFFD55E5D, 0xFFE8A0A0, 0xFFD55E5D];
}
