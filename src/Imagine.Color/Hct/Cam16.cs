namespace Imagine.Color;

/// <summary>Цветовая модель CAM16 (оттенок, хрома, светлота J и координаты UCS).</summary>
public sealed class Cam16
{
    public double Hue { get; }
    public double Chroma { get; }
    public double J { get; }
    public double Q { get; }
    public double M { get; }
    public double S { get; }
    public double Jstar { get; }
    public double Astar { get; }
    public double Bstar { get; }

    private Cam16(double hue, double chroma, double j, double q, double m, double s, double jstar, double astar, double bstar)
    {
        Hue = hue; Chroma = chroma; J = j; Q = q; M = m; S = s; Jstar = jstar; Astar = astar; Bstar = bstar;
    }

    /// <summary>Перцептивное расстояние между цветами (ΔE в CAM16-UCS).</summary>
    public double Distance(Cam16 other)
    {
        double dJ = Jstar - other.Jstar, dA = Astar - other.Astar, dB = Bstar - other.Bstar;
        double dEPrime = Math.Sqrt(dJ * dJ + dA * dA + dB * dB);
        return 1.41 * Math.Pow(dEPrime, 0.63);
    }

    public static Cam16 FromArgb(uint argb) => FromArgbInViewingConditions(argb, ViewingConditions.Default);

    public static Cam16 FromArgbInViewingConditions(uint argb, ViewingConditions vc)
    {
        double redL = ColorUtils.Linearized(ColorUtils.RedFromArgb(argb));
        double greenL = ColorUtils.Linearized(ColorUtils.GreenFromArgb(argb));
        double blueL = ColorUtils.Linearized(ColorUtils.BlueFromArgb(argb));
        double x = 0.41233895 * redL + 0.35762064 * greenL + 0.18051042 * blueL;
        double y = 0.2126 * redL + 0.7152 * greenL + 0.0722 * blueL;
        double z = 0.01932141 * redL + 0.11916382 * greenL + 0.95034478 * blueL;
        return FromXyzInViewingConditions(x, y, z, vc);
    }

    public static Cam16 FromXyzInViewingConditions(double x, double y, double z, ViewingConditions vc)
    {
        double rC = 0.401288 * x + 0.650173 * y - 0.051461 * z;
        double gC = -0.250268 * x + 1.204414 * y + 0.045854 * z;
        double bC = -0.002079 * x + 0.048952 * y + 0.953127 * z;
        double rD = vc.RgbD[0] * rC, gD = vc.RgbD[1] * gC, bD = vc.RgbD[2] * bC;
        double rAF = Math.Pow(vc.Fl * Math.Abs(rD) / 100.0, 0.42);
        double gAF = Math.Pow(vc.Fl * Math.Abs(gD) / 100.0, 0.42);
        double bAF = Math.Pow(vc.Fl * Math.Abs(bD) / 100.0, 0.42);
        double rA = MathUtils.Signum(rD) * 400.0 * rAF / (rAF + 27.13);
        double gA = MathUtils.Signum(gD) * 400.0 * gAF / (gAF + 27.13);
        double bA = MathUtils.Signum(bD) * 400.0 * bAF / (bAF + 27.13);
        double a = (11.0 * rA + -12.0 * gA + bA) / 11.0;
        double b = (rA + gA - 2.0 * bA) / 9.0;
        double u = (20.0 * rA + 20.0 * gA + 21.0 * bA) / 20.0;
        double p2 = (40.0 * rA + 20.0 * gA + bA) / 20.0;
        double atanDegrees = Math.Atan2(b, a) * 180.0 / Math.PI;
        double hue = MathUtils.SanitizeDegreesDouble(atanDegrees);
        double hueRadians = hue * Math.PI / 180.0;
        double ac = p2 * vc.Nbb;
        double j = 100.0 * Math.Pow(ac / vc.Aw, vc.C * vc.Z);
        double q = 4.0 / vc.C * Math.Sqrt(j / 100.0) * (vc.Aw + 4.0) * vc.FlRoot;
        double huePrime = hue < 20.14 ? hue + 360 : hue;
        double eHue = 0.25 * (Math.Cos(huePrime * Math.PI / 180.0 + 2.0) + 3.8);
        double p1 = 50000.0 / 13.0 * eHue * vc.Nc * vc.Ncb;
        double t = p1 * Math.Sqrt(a * a + b * b) / (u + 0.305);
        double alpha = Math.Pow(t, 0.9) * Math.Pow(1.64 - Math.Pow(0.29, vc.N), 0.73);
        double c = alpha * Math.Sqrt(j / 100.0);
        double m = c * vc.FlRoot;
        double s = 50.0 * Math.Sqrt(alpha * vc.C / (vc.Aw + 4.0));
        double jstar = (1.0 + 100.0 * 0.007) * j / (1.0 + 0.007 * j);
        double mstar = 1.0 / 0.0228 * Math.Log(1.0 + 0.0228 * m);
        return new Cam16(hue, c, j, q, m, s, jstar, mstar * Math.Cos(hueRadians), mstar * Math.Sin(hueRadians));
    }

    public static Cam16 FromJch(double j, double c, double h) => FromJchInViewingConditions(j, c, h, ViewingConditions.Default);

    public static Cam16 FromJchInViewingConditions(double j, double c, double h, ViewingConditions vc)
    {
        double q = 4.0 / vc.C * Math.Sqrt(j / 100.0) * (vc.Aw + 4.0) * vc.FlRoot;
        double m = c * vc.FlRoot;
        double alpha = c / Math.Sqrt(j / 100.0);
        double s = 50.0 * Math.Sqrt(alpha * vc.C / (vc.Aw + 4.0));
        double hueRadians = h * Math.PI / 180.0;
        double jstar = (1.0 + 100.0 * 0.007) * j / (1.0 + 0.007 * j);
        double mstar = 1.0 / 0.0228 * Math.Log(1.0 + 0.0228 * m);
        return new Cam16(h, c, j, q, m, s, jstar, mstar * Math.Cos(hueRadians), mstar * Math.Sin(hueRadians));
    }

    public static Cam16 FromUcs(double jstar, double astar, double bstar)
    {
        var vc = ViewingConditions.Default;
        double m = Math.Sqrt(astar * astar + bstar * bstar);
        double bigM = (Math.Exp(m * 0.0228) - 1.0) / 0.0228;
        double c = bigM / vc.FlRoot;
        double h = Math.Atan2(bstar, astar) * (180.0 / Math.PI);
        if (h < 0.0) h += 360.0;
        double j = jstar / (1 - (jstar - 100) * 0.007);
        return FromJchInViewingConditions(j, c, h, vc);
    }

    public uint ToArgb() => Viewed(ViewingConditions.Default);

    public uint Viewed(ViewingConditions vc)
    {
        var xyz = XyzInViewingConditions(vc);
        return ColorUtils.ArgbFromXyz(xyz[0], xyz[1], xyz[2]);
    }

    public double[] XyzInViewingConditions(ViewingConditions vc)
    {
        double alpha = Chroma == 0.0 || J == 0.0 ? 0.0 : Chroma / Math.Sqrt(J / 100.0);
        double t = Math.Pow(alpha / Math.Pow(1.64 - Math.Pow(0.29, vc.N), 0.73), 1.0 / 0.9);
        double hRad = Hue * Math.PI / 180.0;
        double eHue = 0.25 * (Math.Cos(hRad + 2.0) + 3.8);
        double ac = vc.Aw * Math.Pow(J / 100.0, 1.0 / vc.C / vc.Z);
        double p1 = eHue * (50000.0 / 13.0) * vc.Nc * vc.Ncb;
        double p2 = ac / vc.Nbb;
        double hSin = Math.Sin(hRad), hCos = Math.Cos(hRad);
        double gamma = 23.0 * (p2 + 0.305) * t / (23.0 * p1 + 11 * t * hCos + 108.0 * t * hSin);
        double a = gamma * hCos, b = gamma * hSin;
        double rA = (460.0 * p2 + 451.0 * a + 288.0 * b) / 1403.0;
        double gA = (460.0 * p2 - 891.0 * a - 261.0 * b) / 1403.0;
        double bA = (460.0 * p2 - 220.0 * a - 6300.0 * b) / 1403.0;
        double Inv(double v) => MathUtils.Signum(v) * (100.0 / vc.Fl) * Math.Pow(Math.Max(0, 27.13 * Math.Abs(v) / (400.0 - Math.Abs(v))), 1.0 / 0.42);
        double rF = Inv(rA) / vc.RgbD[0], gF = Inv(gA) / vc.RgbD[1], bF = Inv(bA) / vc.RgbD[2];
        return
        [
            1.86206786 * rF - 1.01125463 * gF + 0.14918677 * bF,
            0.38752654 * rF + 0.62144744 * gF - 0.00897398 * bF,
            -0.01584150 * rF - 0.03412294 * gF + 1.04996444 * bF,
        ];
    }
}
