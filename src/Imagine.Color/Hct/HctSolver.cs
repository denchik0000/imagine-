namespace Imagine.Color;

/// <summary>
/// Решатель HCT → sRGB: находит цвет с заданными оттенком, хромой и тоном,
/// а если такой хромы нет в гамме sRGB — ближайший по хроме цвет на её границе.
/// </summary>
internal static partial class HctSolver
{
    private static double SanitizeRadians(double angle) => (angle + Math.PI * 8) % (Math.PI * 2);

    private static double TrueDelinearized(double rgbComponent)
    {
        double normalized = rgbComponent / 100.0;
        double d = normalized <= 0.0031308 ? normalized * 12.92 : 1.055 * Math.Pow(normalized, 1.0 / 2.4) - 0.055;
        return d * 255.0;
    }

    private static double ChromaticAdaptation(double component)
    {
        double af = Math.Pow(Math.Abs(component), 0.42);
        return MathUtils.Signum(component) * 400.0 * af / (af + 27.13);
    }

    private static double HueOf(double[] linrgb)
    {
        var sd = MathUtils.MatrixMultiply(linrgb, ScaledDiscountFromLinrgb);
        double rA = ChromaticAdaptation(sd[0]);
        double gA = ChromaticAdaptation(sd[1]);
        double bA = ChromaticAdaptation(sd[2]);
        double a = (11.0 * rA + -12.0 * gA + bA) / 11.0;
        double b = (rA + gA - 2.0 * bA) / 9.0;
        return Math.Atan2(b, a);
    }

    private static bool AreInCyclicOrder(double a, double b, double c)
        => SanitizeRadians(b - a) < SanitizeRadians(c - a);

    private static double Intercept(double source, double mid, double target) => (mid - source) / (target - source);

    private static double[] LerpPoint(double[] s, double t, double[] e) =>
        [s[0] + (e[0] - s[0]) * t, s[1] + (e[1] - s[1]) * t, s[2] + (e[2] - s[2]) * t];

    private static double[] SetCoordinate(double[] source, double coordinate, double[] target, int axis)
        => LerpPoint(source, Intercept(source[axis], coordinate, target[axis]), target);

    private static bool IsBounded(double x) => x >= 0.0 && x <= 100.0;

    private static double[] NthVertex(double y, int n)
    {
        double kR = YFromLinrgb[0], kG = YFromLinrgb[1], kB = YFromLinrgb[2];
        double coordA = n % 4 <= 1 ? 0.0 : 100.0;
        double coordB = n % 2 == 0 ? 0.0 : 100.0;
        if (n < 4)
        {
            double g = coordA, b = coordB, r = (y - g * kG - b * kB) / kR;
            return IsBounded(r) ? [r, g, b] : [-1.0, -1.0, -1.0];
        }
        if (n < 8)
        {
            double b = coordA, r = coordB, g = (y - r * kR - b * kB) / kG;
            return IsBounded(g) ? [r, g, b] : [-1.0, -1.0, -1.0];
        }
        {
            double r = coordA, g = coordB, b = (y - r * kR - g * kG) / kB;
            return IsBounded(b) ? [r, g, b] : [-1.0, -1.0, -1.0];
        }
    }

    private static (double[] Left, double[] Right) BisectToSegment(double y, double targetHue)
    {
        double[] left = [-1.0, -1.0, -1.0];
        double[] right = left;
        double leftHue = 0.0, rightHue = 0.0;
        bool initialized = false, uncut = true;
        for (int n = 0; n < 12; n++)
        {
            var mid = NthVertex(y, n);
            if (mid[0] < 0) continue;
            double midHue = HueOf(mid);
            if (!initialized)
            {
                left = mid; right = mid; leftHue = midHue; rightHue = midHue; initialized = true;
                continue;
            }
            if (uncut || AreInCyclicOrder(leftHue, midHue, rightHue))
            {
                uncut = false;
                if (AreInCyclicOrder(leftHue, targetHue, midHue)) { right = mid; rightHue = midHue; }
                else { left = mid; leftHue = midHue; }
            }
        }
        return (left, right);
    }

    private static double[] Midpoint(double[] a, double[] b) => [(a[0] + b[0]) / 2, (a[1] + b[1]) / 2, (a[2] + b[2]) / 2];

    private static int CriticalPlaneBelow(double x) => (int)Math.Floor(x - 0.5);

    private static int CriticalPlaneAbove(double x) => (int)Math.Ceiling(x - 0.5);

    private static double[] BisectToLimit(double y, double targetHue)
    {
        var (left, right) = BisectToSegment(y, targetHue);
        double leftHue = HueOf(left);
        for (int axis = 0; axis < 3; axis++)
        {
            if (left[axis] == right[axis]) continue;
            int lPlane, rPlane;
            if (left[axis] < right[axis])
            {
                lPlane = CriticalPlaneBelow(TrueDelinearized(left[axis]));
                rPlane = CriticalPlaneAbove(TrueDelinearized(right[axis]));
            }
            else
            {
                lPlane = CriticalPlaneAbove(TrueDelinearized(left[axis]));
                rPlane = CriticalPlaneBelow(TrueDelinearized(right[axis]));
            }
            for (int i = 0; i < 8; i++)
            {
                if (Math.Abs(rPlane - lPlane) <= 1) break;
                int mPlane = (int)Math.Floor((lPlane + rPlane) / 2.0);
                double midPlaneCoordinate = CriticalPlanes[mPlane];
                var mid = SetCoordinate(left, midPlaneCoordinate, right, axis);
                double midHue = HueOf(mid);
                if (AreInCyclicOrder(leftHue, targetHue, midHue)) { right = mid; rPlane = mPlane; }
                else { left = mid; leftHue = midHue; lPlane = mPlane; }
            }
        }
        return Midpoint(left, right);
    }

    private static double InverseChromaticAdaptation(double adapted)
    {
        double adaptedAbs = Math.Abs(adapted);
        double @base = Math.Max(0, 27.13 * adaptedAbs / (400.0 - adaptedAbs));
        return MathUtils.Signum(adapted) * Math.Pow(@base, 1.0 / 0.42);
    }

    private static uint FindResultByJ(double hueRadians, double chroma, double y)
    {
        double j = Math.Sqrt(y) * 11.0;
        var vc = ViewingConditions.Default;
        double tInnerCoeff = 1 / Math.Pow(1.64 - Math.Pow(0.29, vc.N), 0.73);
        double eHue = 0.25 * (Math.Cos(hueRadians + 2.0) + 3.8);
        double p1 = eHue * (50000.0 / 13.0) * vc.Nc * vc.Ncb;
        double hSin = Math.Sin(hueRadians), hCos = Math.Cos(hueRadians);
        for (int round = 0; round < 5; round++)
        {
            double jNormalized = j / 100.0;
            double alpha = chroma == 0.0 || j == 0.0 ? 0.0 : chroma / Math.Sqrt(jNormalized);
            double t = Math.Pow(alpha * tInnerCoeff, 1.0 / 0.9);
            double ac = vc.Aw * Math.Pow(jNormalized, 1.0 / vc.C / vc.Z);
            double p2 = ac / vc.Nbb;
            double gamma = 23.0 * (p2 + 0.305) * t / (23.0 * p1 + 11 * t * hCos + 108.0 * t * hSin);
            double a = gamma * hCos, b = gamma * hSin;
            double rA = (460.0 * p2 + 451.0 * a + 288.0 * b) / 1403.0;
            double gA = (460.0 * p2 - 891.0 * a - 261.0 * b) / 1403.0;
            double bA = (460.0 * p2 - 220.0 * a - 6300.0 * b) / 1403.0;
            var linrgb = MathUtils.MatrixMultiply(
                [InverseChromaticAdaptation(rA), InverseChromaticAdaptation(gA), InverseChromaticAdaptation(bA)],
                LinrgbFromScaledDiscount);
            if (linrgb[0] < 0 || linrgb[1] < 0 || linrgb[2] < 0) return 0;
            double fnj = YFromLinrgb[0] * linrgb[0] + YFromLinrgb[1] * linrgb[1] + YFromLinrgb[2] * linrgb[2];
            if (fnj <= 0) return 0;
            if (round == 4 || Math.Abs(fnj - y) < 0.002)
            {
                if (linrgb[0] > 100.01 || linrgb[1] > 100.01 || linrgb[2] > 100.01) return 0;
                return ColorUtils.ArgbFromLinrgb(linrgb);
            }
            j -= (fnj - y) * j / (2 * fnj);
        }
        return 0;
    }

    public static uint SolveToArgb(double hueDegrees, double chroma, double lstar)
    {
        if (chroma < 0.0001 || lstar < 0.0001 || lstar > 99.9999) return ColorUtils.ArgbFromLstar(lstar);
        hueDegrees = MathUtils.SanitizeDegreesDouble(hueDegrees);
        double hueRadians = hueDegrees / 180 * Math.PI;
        double y = ColorUtils.YFromLstar(lstar);
        uint exact = FindResultByJ(hueRadians, chroma, y);
        if (exact != 0) return exact;
        return ColorUtils.ArgbFromLinrgb(BisectToLimit(y, hueRadians));
    }
}
