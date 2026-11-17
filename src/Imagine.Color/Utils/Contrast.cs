namespace Imagine.Color;

/// <summary>Контраст по WCAG, выраженный через тоны HCT.</summary>
public static class Contrast
{
    public static double RatioOfTones(double toneA, double toneB)
    {
        toneA = MathUtils.ClampDouble(0.0, 100.0, toneA);
        toneB = MathUtils.ClampDouble(0.0, 100.0, toneB);
        return RatioOfYs(ColorUtils.YFromLstar(toneA), ColorUtils.YFromLstar(toneB));
    }

    public static double RatioOfYs(double y1, double y2)
    {
        double lighter = Math.Max(y1, y2);
        double darker = lighter == y2 ? y1 : y2;
        return (lighter + 5.0) / (darker + 5.0);
    }

    /// <summary>Контраст двух ARGB-цветов (1..21).</summary>
    public static double RatioOfArgb(uint a, uint b) => RatioOfTones(ColorUtils.LstarFromArgb(a), ColorUtils.LstarFromArgb(b));

    public static double Lighter(double tone, double ratio)
    {
        if (tone < 0.0 || tone > 100.0) return -1.0;
        double darkY = ColorUtils.YFromLstar(tone);
        double lightY = ratio * (darkY + 5.0) - 5.0;
        double realContrast = RatioOfYs(lightY, darkY);
        double delta = Math.Abs(realContrast - ratio);
        if (realContrast < ratio && delta > 0.04) return -1.0;
        double value = ColorUtils.LstarFromY(lightY) + 0.4;
        return value < 0 || value > 100 ? -1.0 : value;
    }

    public static double Darker(double tone, double ratio)
    {
        if (tone < 0.0 || tone > 100.0) return -1.0;
        double lightY = ColorUtils.YFromLstar(tone);
        double darkY = (lightY + 5.0) / ratio - 5.0;
        double realContrast = RatioOfYs(lightY, darkY);
        double delta = Math.Abs(realContrast - ratio);
        if (realContrast < ratio && delta > 0.04) return -1.0;
        double value = ColorUtils.LstarFromY(darkY) - 0.4;
        return value < 0.0 || value > 100.0 ? -1.0 : value;
    }

    public static double LighterUnsafe(double tone, double ratio)
    {
        double safe = Lighter(tone, ratio);
        return safe < 0.0 ? 100.0 : safe;
    }

    public static double DarkerUnsafe(double tone, double ratio)
    {
        double safe = Darker(tone, ratio);
        return safe < 0.0 ? 0.0 : safe;
    }
}
