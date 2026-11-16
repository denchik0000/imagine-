namespace Imagine.Color;

/// <summary>
/// Цветовая схема, построенная из одного «затравочного» цвета: шесть тональных палитр
/// и правила, по которым из них берутся цвета ролей (<see cref="MaterialDynamicColors"/>).
/// </summary>
public sealed class DynamicScheme
{
    public Hct SourceColorHct { get; }
    public uint SourceColorArgb => SourceColorHct.Argb;
    public Variant Variant { get; }
    public bool IsDark { get; }
    /// <summary>−1 … 1: −1 — пониженный, 0 — стандартный, 0.5 — средний, 1 — высокий контраст.</summary>
    public double ContrastLevel { get; }

    public TonalPalette PrimaryPalette { get; }
    public TonalPalette SecondaryPalette { get; }
    public TonalPalette TertiaryPalette { get; }
    public TonalPalette NeutralPalette { get; }
    public TonalPalette NeutralVariantPalette { get; }
    public TonalPalette ErrorPalette { get; }

    public DynamicScheme(Hct sourceColorHct, Variant variant, bool isDark, double contrastLevel = 0.0)
    {
        SourceColorHct = sourceColorHct;
        Variant = variant;
        IsDark = isDark;
        ContrastLevel = MathUtils.ClampDouble(-1, 1, contrastLevel);
        var s = sourceColorHct;
        PrimaryPalette = variant switch
        {
            Variant.Content or Variant.Fidelity => TonalPalette.FromHueAndChroma(s.Hue, s.Chroma),
            Variant.FruitSalad => TonalPalette.FromHueAndChroma(MathUtils.SanitizeDegreesDouble(s.Hue - 50.0), 48.0),
            Variant.Monochrome => TonalPalette.FromHueAndChroma(s.Hue, 0.0),
            Variant.Neutral => TonalPalette.FromHueAndChroma(s.Hue, 12.0),
            Variant.Rainbow => TonalPalette.FromHueAndChroma(s.Hue, 48.0),
            Variant.TonalSpot => TonalPalette.FromHueAndChroma(s.Hue, 36.0),
            Variant.Expressive => TonalPalette.FromHueAndChroma(MathUtils.SanitizeDegreesDouble(s.Hue + 240), 40),
            Variant.Vibrant => TonalPalette.FromHueAndChroma(s.Hue, 200.0),
            _ => throw new ArgumentOutOfRangeException(nameof(variant)),
        };
        SecondaryPalette = variant switch
        {
            Variant.Content or Variant.Fidelity => TonalPalette.FromHueAndChroma(s.Hue, Math.Max(s.Chroma - 32.0, s.Chroma * 0.5)),
            Variant.FruitSalad => TonalPalette.FromHueAndChroma(MathUtils.SanitizeDegreesDouble(s.Hue - 50.0), 36.0),
            Variant.Monochrome => TonalPalette.FromHueAndChroma(s.Hue, 0.0),
            Variant.Neutral => TonalPalette.FromHueAndChroma(s.Hue, 8.0),
            Variant.Rainbow or Variant.TonalSpot => TonalPalette.FromHueAndChroma(s.Hue, 16.0),
            Variant.Expressive => TonalPalette.FromHueAndChroma(
                GetRotatedHue(s, [0, 21, 51, 121, 151, 191, 271, 321, 360], [45, 95, 45, 20, 45, 90, 45, 45, 45]), 24.0),
            Variant.Vibrant => TonalPalette.FromHueAndChroma(
                GetRotatedHue(s, [0, 41, 61, 101, 131, 181, 251, 301, 360], [18, 15, 10, 12, 15, 18, 15, 12, 12]), 24.0),
            _ => throw new ArgumentOutOfRangeException(nameof(variant)),
        };
        TertiaryPalette = variant switch
        {
            Variant.Content => TonalPalette.FromHct(DislikeAnalyzer.FixIfDisliked(new TemperatureCache(s).Analogous(3, 6)[2])),
            Variant.Fidelity => TonalPalette.FromHct(DislikeAnalyzer.FixIfDisliked(new TemperatureCache(s).Complement)),
            Variant.FruitSalad => TonalPalette.FromHueAndChroma(s.Hue, 36.0),
            Variant.Monochrome => TonalPalette.FromHueAndChroma(s.Hue, 0.0),
            Variant.Neutral => TonalPalette.FromHueAndChroma(s.Hue, 16.0),
            Variant.Rainbow or Variant.TonalSpot => TonalPalette.FromHueAndChroma(MathUtils.SanitizeDegreesDouble(s.Hue + 60.0), 24.0),
            Variant.Expressive => TonalPalette.FromHueAndChroma(
                GetRotatedHue(s, [0, 21, 51, 121, 151, 191, 271, 321, 360], [120, 120, 20, 45, 20, 15, 20, 120, 120]), 32.0),
            Variant.Vibrant => TonalPalette.FromHueAndChroma(
                GetRotatedHue(s, [0, 41, 61, 101, 131, 181, 251, 301, 360], [35, 30, 20, 25, 30, 35, 30, 25, 25]), 32.0),
            _ => throw new ArgumentOutOfRangeException(nameof(variant)),
        };
        NeutralPalette = variant switch
        {
            Variant.Content or Variant.Fidelity => TonalPalette.FromHueAndChroma(s.Hue, s.Chroma / 8.0),
            Variant.FruitSalad => TonalPalette.FromHueAndChroma(s.Hue, 10.0),
            Variant.Monochrome or Variant.Rainbow => TonalPalette.FromHueAndChroma(s.Hue, 0.0),
            Variant.Neutral => TonalPalette.FromHueAndChroma(s.Hue, 2.0),
            Variant.TonalSpot => TonalPalette.FromHueAndChroma(s.Hue, 6.0),
            Variant.Expressive => TonalPalette.FromHueAndChroma(MathUtils.SanitizeDegreesDouble(s.Hue + 15), 8),
            Variant.Vibrant => TonalPalette.FromHueAndChroma(s.Hue, 10),
            _ => throw new ArgumentOutOfRangeException(nameof(variant)),
        };
        NeutralVariantPalette = variant switch
        {
            Variant.Content or Variant.Fidelity => TonalPalette.FromHueAndChroma(s.Hue, s.Chroma / 8.0 + 4.0),
            Variant.FruitSalad => TonalPalette.FromHueAndChroma(s.Hue, 16.0),
            Variant.Monochrome or Variant.Rainbow => TonalPalette.FromHueAndChroma(s.Hue, 0.0),
            Variant.Neutral => TonalPalette.FromHueAndChroma(s.Hue, 2.0),
            Variant.TonalSpot => TonalPalette.FromHueAndChroma(s.Hue, 8.0),
            Variant.Expressive => TonalPalette.FromHueAndChroma(MathUtils.SanitizeDegreesDouble(s.Hue + 15), 12),
            Variant.Vibrant => TonalPalette.FromHueAndChroma(s.Hue, 12),
            _ => throw new ArgumentOutOfRangeException(nameof(variant)),
        };
        ErrorPalette = TonalPalette.FromHueAndChroma(25.0, 84.0);
    }

    public DynamicScheme(uint sourceArgb, Variant variant, bool isDark, double contrastLevel = 0.0)
        : this(Hct.FromArgb(sourceArgb), variant, isDark, contrastLevel)
    {
    }

    public static double GetPiecewiseHue(Hct source, double[] hueBreakpoints, double[] hues)
    {
        int size = Math.Min(hueBreakpoints.Length - 1, hues.Length);
        double sourceHue = source.Hue;
        for (int i = 0; i < size; i++)
        {
            if (sourceHue >= hueBreakpoints[i] && sourceHue < hueBreakpoints[i + 1])
                return MathUtils.SanitizeDegreesDouble(hues[i]);
        }
        return sourceHue;
    }

    public static double GetRotatedHue(Hct source, double[] hueBreakpoints, double[] rotations)
    {
        double rotation = GetPiecewiseHue(source, hueBreakpoints, rotations);
        if (Math.Min(hueBreakpoints.Length - 1, rotations.Length) <= 0) rotation = 0;
        return MathUtils.SanitizeDegreesDouble(source.Hue + rotation);
    }

    public uint Get(ColorRole role) => MaterialDynamicColors.Get(role).GetArgb(this);

    public uint Get(DynamicColor color) => color.GetArgb(this);

    /// <summary>Все роли схемы разом.</summary>
    public IReadOnlyDictionary<ColorRole, uint> ToDictionary()
        => Enum.GetValues<ColorRole>().ToDictionary(r => r, Get);

    public override string ToString()
        => $"Scheme: variant={Variant}, mode={(IsDark ? "dark" : "light")}, contrast={ContrastLevel:0.0}, seed={SourceColorHct}";
}
