namespace Imagine.Color;

/// <summary>Смешение цветов в пространстве HCT/CAM16.</summary>
public static class Blend
{
    /// <summary>
    /// Слегка поворачивает оттенок <paramref name="designColor"/> к <paramref name="sourceColor"/> (не более 15°),
    /// чтобы фирменный или семантический цвет гармонировал с палитрой обоев.
    /// </summary>
    public static uint Harmonize(uint designColor, uint sourceColor)
    {
        var from = Hct.FromArgb(designColor);
        var to = Hct.FromArgb(sourceColor);
        double differenceDegrees = MathUtils.DifferenceDegrees(from.Hue, to.Hue);
        double rotationDegrees = Math.Min(differenceDegrees * 0.5, 15.0);
        double outputHue = MathUtils.SanitizeDegreesDouble(from.Hue + rotationDegrees * MathUtils.RotationDirection(from.Hue, to.Hue));
        return Hct.From(outputHue, from.Chroma, from.Tone).Argb;
    }

    /// <summary>Смешивает оттенок, сохраняя хрому и тон исходного цвета.</summary>
    public static uint HctHue(uint from, uint to, double amount)
    {
        uint ucs = Cam16Ucs(from, to, amount);
        var ucsCam = Cam16.FromArgb(ucs);
        var fromCam = Cam16.FromArgb(from);
        return Hct.From(ucsCam.Hue, fromCam.Chroma, ColorUtils.LstarFromArgb(from)).Argb;
    }

    /// <summary>Перцептивно равномерное смешение в CAM16-UCS.</summary>
    public static uint Cam16Ucs(uint from, uint to, double amount)
    {
        var a = Cam16.FromArgb(from);
        var b = Cam16.FromArgb(to);
        double jstar = a.Jstar + (b.Jstar - a.Jstar) * amount;
        double astar = a.Astar + (b.Astar - a.Astar) * amount;
        double bstar = a.Bstar + (b.Bstar - a.Bstar) * amount;
        return Cam16.FromUcs(jstar, astar, bstar).ToArgb();
    }
}
