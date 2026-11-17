using Imagine.Color;

namespace Imagine.Core.Theming;

/// <summary>
/// Параметры «матового стекла» и светящейся обводки (Contour Glow) в духе ColorOS 17.
/// </summary>
public sealed record GlassTokens
{
    /// <summary>Цвет тонировки стекла (ARGB с альфой).</summary>
    public required uint Tint { get; init; }
    /// <summary>Плотность тонировки 0..1.</summary>
    public required double TintOpacity { get; init; }
    /// <summary>Сила «люминесценции» акрила 0..1.</summary>
    public required double LuminosityOpacity { get; init; }
    /// <summary>Радиус размытия, px.</summary>
    public required double BlurRadius { get; init; }
    /// <summary>Цвета градиента обводки (по кругу).</summary>
    public required IReadOnlyList<uint> GlowStops { get; init; }
    /// <summary>Яркость свечения 0..1.</summary>
    public required double GlowIntensity { get; init; }
    /// <summary>Толщина обводки, px.</summary>
    public required double GlowThickness { get; init; }
    /// <summary>Цвет тонкого блика по верхней кромке.</summary>
    public required uint Highlight { get; init; }
    public required double CornerRadius { get; init; }

    public static GlassTokens From(DynamicScheme scheme, GlassStyle style)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        ArgumentNullException.ThrowIfNull(style);
        uint surface = scheme.Get(ColorRole.SurfaceContainer);
        uint primary = scheme.Get(ColorRole.Primary);
        uint tertiary = scheme.Get(ColorRole.Tertiary);
        uint secondary = scheme.Get(ColorRole.SecondaryContainer);
        double tint = style.Frost * (scheme.IsDark ? 0.62 : 0.55);
        return new GlassTokens
        {
            Tint = ColorUtils.WithAlpha(surface, tint),
            TintOpacity = tint,
            LuminosityOpacity = MathUtils.ClampDouble(0, 1, 0.35 + style.Frost * 0.4),
            BlurRadius = 18 + style.Frost * 42,
            GlowStops = style.GlowMode switch
            {
                GlowMode.Off => [],
                GlowMode.Accent => [primary, ColorUtils.WithAlpha(primary, 0.35), primary],
                GlowMode.Aurora => [primary, tertiary, secondary, primary],
                _ => [primary, tertiary, primary],
            },
            GlowIntensity = style.GlowMode == GlowMode.Off ? 0 : style.GlowIntensity,
            GlowThickness = style.GlowThickness,
            Highlight = ColorUtils.WithAlpha(0xFFFFFFFF, scheme.IsDark ? 0.10 : 0.45),
            CornerRadius = style.CornerRadius,
        };
    }
}

public enum GlowMode
{
    Off,
    /// <summary>Однотонная обводка цвета акцента.</summary>
    Accent,
    /// <summary>Акцент → третичный → акцент.</summary>
    Duotone,
    /// <summary>Переливающееся «северное сияние» из трёх цветов схемы.</summary>
    Aurora,
}

/// <summary>Пользовательские настройки стекла.</summary>
public sealed record GlassStyle
{
    /// <summary>0 — почти прозрачное, 1 — сильно матовое.</summary>
    public double Frost { get; init; } = 0.6;
    public GlowMode GlowMode { get; init; } = GlowMode.Duotone;
    public double GlowIntensity { get; init; } = 0.7;
    public double GlowThickness { get; init; } = 1.5;
    public double CornerRadius { get; init; } = 24;
    /// <summary>Блик следует за курсором (Condensed light).</summary>
    public bool CursorLight { get; init; } = true;
}
