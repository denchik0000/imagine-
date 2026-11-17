using System.Globalization;

namespace Imagine.Color;

/// <summary>
/// Преобразования sRGB ⇄ XYZ ⇄ L*a*b* и работа с упакованными ARGB-цветами (0xAARRGGBB).
/// </summary>
public static class ColorUtils
{
    private static readonly double[,] SrgbToXyz =
    {
        { 0.41233895, 0.35762064, 0.18051042 },
        { 0.2126, 0.7152, 0.0722 },
        { 0.01932141, 0.11916382, 0.95034478 },
    };

    private static readonly double[,] XyzToSrgb =
    {
        { 3.2413774792388685, -1.5376652402851851, -0.49885366846268053 },
        { -0.9691452513005321, 1.8758853451067872, 0.04156585616912061 },
        { 0.05562093689691305, -0.20395524564742123, 1.0571799111220335 },
    };

    public static readonly double[] WhitePointD65 = [95.047, 100.0, 108.883];

    public static uint ArgbFromRgb(int red, int green, int blue)
        => 0xFF000000u | ((uint)(red & 255) << 16) | ((uint)(green & 255) << 8) | (uint)(blue & 255);

    public static uint ArgbFromLinrgb(double[] linrgb)
        => ArgbFromRgb(Delinearized(linrgb[0]), Delinearized(linrgb[1]), Delinearized(linrgb[2]));

    public static int AlphaFromArgb(uint argb) => (int)((argb >> 24) & 255);
    public static int RedFromArgb(uint argb) => (int)((argb >> 16) & 255);
    public static int GreenFromArgb(uint argb) => (int)((argb >> 8) & 255);
    public static int BlueFromArgb(uint argb) => (int)(argb & 255);
    public static bool IsOpaque(uint argb) => AlphaFromArgb(argb) >= 255;

    public static uint ArgbFromXyz(double x, double y, double z)
    {
        var m = XyzToSrgb;
        double r = m[0, 0] * x + m[0, 1] * y + m[0, 2] * z;
        double g = m[1, 0] * x + m[1, 1] * y + m[1, 2] * z;
        double b = m[2, 0] * x + m[2, 1] * y + m[2, 2] * z;
        return ArgbFromRgb(Delinearized(r), Delinearized(g), Delinearized(b));
    }

    public static double[] XyzFromArgb(uint argb)
        => MathUtils.MatrixMultiply(
            [Linearized(RedFromArgb(argb)), Linearized(GreenFromArgb(argb)), Linearized(BlueFromArgb(argb))],
            SrgbToXyz);

    public static uint ArgbFromLab(double l, double a, double b)
    {
        double fy = (l + 16.0) / 116.0;
        double fx = a / 500.0 + fy;
        double fz = fy - b / 200.0;
        return ArgbFromXyz(LabInvf(fx) * WhitePointD65[0], LabInvf(fy) * WhitePointD65[1], LabInvf(fz) * WhitePointD65[2]);
    }

    public static double[] LabFromArgb(uint argb)
    {
        var xyz = XyzFromArgb(argb);
        double fx = LabF(xyz[0] / WhitePointD65[0]);
        double fy = LabF(xyz[1] / WhitePointD65[1]);
        double fz = LabF(xyz[2] / WhitePointD65[2]);
        return [116.0 * fy - 16.0, 500.0 * (fx - fy), 200.0 * (fy - fz)];
    }

    public static uint ArgbFromLstar(double lstar)
    {
        int component = Delinearized(YFromLstar(lstar));
        return ArgbFromRgb(component, component, component);
    }

    public static double LstarFromArgb(uint argb) => 116.0 * LabF(XyzFromArgb(argb)[1] / 100.0) - 16.0;

    public static double YFromLstar(double lstar) => 100.0 * LabInvf((lstar + 16.0) / 116.0);

    public static double LstarFromY(double y) => LabF(y / 100.0) * 116.0 - 16.0;

    public static double Linearized(int rgbComponent)
    {
        double normalized = rgbComponent / 255.0;
        return normalized <= 0.040449936
            ? normalized / 12.92 * 100.0
            : Math.Pow((normalized + 0.055) / 1.055, 2.4) * 100.0;
    }

    public static int Delinearized(double rgbComponent)
    {
        double normalized = rgbComponent / 100.0;
        double d = normalized <= 0.0031308
            ? normalized * 12.92
            : 1.055 * Math.Pow(normalized, 1.0 / 2.4) - 0.055;
        return MathUtils.ClampInt(0, 255, MathUtils.RoundHalfUp(d * 255.0));
    }

    public static double LabF(double t)
    {
        const double e = 216.0 / 24389.0;
        const double kappa = 24389.0 / 27.0;
        return t > e ? Math.Pow(t, 1.0 / 3.0) : (kappa * t + 16) / 116;
    }

    public static double LabInvf(double ft)
    {
        const double e = 216.0 / 24389.0;
        const double kappa = 24389.0 / 27.0;
        double ft3 = ft * ft * ft;
        return ft3 > e ? ft3 : (116 * ft - 16) / kappa;
    }

    /// <summary>Относительная яркость по WCAG (0..1).</summary>
    public static double RelativeLuminance(uint argb) => XyzFromArgb(argb)[1] / 100.0;

    /// <summary>«#RRGGBB».</summary>
    public static string ToHex(uint argb) => "#" + (argb & 0xFFFFFF).ToString("X6", CultureInfo.InvariantCulture);

    /// <summary>«#AARRGGBB».</summary>
    public static string ToHexWithAlpha(uint argb) => "#" + argb.ToString("X8", CultureInfo.InvariantCulture);

    /// <summary>Разбирает «#RGB», «#RRGGBB» или «#AARRGGBB».</summary>
    public static uint FromHex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        var s = hex.Trim().TrimStart('#');
        if (s.Length == 3) s = string.Concat(s[0], s[0], s[1], s[1], s[2], s[2]);
        if (s.Length == 6) s = "FF" + s;
        if (s.Length != 8 || !uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
            throw new FormatException($"Не удалось разобрать цвет «{hex}».");
        return v;
    }

    public static bool TryFromHex(string? hex, out uint argb)
    {
        argb = 0;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        try { argb = FromHex(hex); return true; }
        catch (FormatException) { return false; }
    }

    /// <summary>Меняет альфа-канал цвета.</summary>
    public static uint WithAlpha(uint argb, double alpha)
        => ((uint)MathUtils.ClampInt(0, 255, MathUtils.RoundHalfUp(alpha * 255)) << 24) | (argb & 0xFFFFFF);

    /// <summary>Линейное смешение двух цветов в sRGB.</summary>
    public static uint Mix(uint a, uint b, double t)
    {
        t = MathUtils.ClampDouble(0, 1, t);
        int Ch(int x, int y) => MathUtils.RoundHalfUp(x + (y - x) * t);
        int alpha = Ch(AlphaFromArgb(a), AlphaFromArgb(b));
        return ((uint)alpha << 24) | (ArgbFromRgb(Ch(RedFromArgb(a), RedFromArgb(b)), Ch(GreenFromArgb(a), GreenFromArgb(b)), Ch(BlueFromArgb(a), BlueFromArgb(b))) & 0xFFFFFF);
    }
}
