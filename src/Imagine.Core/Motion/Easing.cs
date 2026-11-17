namespace Imagine.Core.Motion;

/// <summary>Кривые сглаживания (t от 0 до 1).</summary>
public static class Easing
{
    public static double Linear(double t) => Clamp(t);

    /// <summary>Fluent «decelerate»: быстрый старт, мягкий финиш.</summary>
    public static double Decelerate(double t)
    {
        t = Clamp(t);
        return 1 - Math.Pow(1 - t, 3);
    }

    public static double Accelerate(double t)
    {
        t = Clamp(t);
        return t * t * t;
    }

    /// <summary>Material «emphasized».</summary>
    public static double Emphasized(double t) => CubicBezier(0.2, 0, 0, 1, t);

    public static double Standard(double t) => CubicBezier(0.2, 0, 0, 1, t);

    /// <summary>Кубическая кривая Безье с точками (0,0), (x1,y1), (x2,y2), (1,1), как в CSS.</summary>
    public static double CubicBezier(double x1, double y1, double x2, double y2, double t)
    {
        t = Clamp(t);
        // Ищем параметр u, при котором x(u)=t, методом Ньютона с запасным делением пополам.
        double u = t;
        for (int i = 0; i < 8; i++)
        {
            double x = Bezier(u, x1, x2) - t;
            double dx = BezierDerivative(u, x1, x2);
            if (Math.Abs(x) < 1e-7) break;
            if (Math.Abs(dx) < 1e-6) break;
            u -= x / dx;
        }
        if (u < 0 || u > 1 || Math.Abs(Bezier(u, x1, x2) - t) > 1e-5)
        {
            double lo = 0, hi = 1;
            u = t;
            for (int i = 0; i < 40; i++)
            {
                double x = Bezier(u, x1, x2);
                if (Math.Abs(x - t) < 1e-7) break;
                if (x < t) lo = u; else hi = u;
                u = (lo + hi) / 2;
            }
        }
        return Bezier(u, y1, y2);
    }

    private static double Bezier(double u, double p1, double p2)
    {
        double v = 1 - u;
        return 3 * v * v * u * p1 + 3 * v * u * u * p2 + u * u * u;
    }

    private static double BezierDerivative(double u, double p1, double p2)
    {
        double v = 1 - u;
        return 3 * v * v * p1 + 6 * v * u * (p2 - p1) + 3 * u * u * (1 - p2);
    }

    /// <summary>«Резиновое» сопротивление за пределами диапазона, как overscroll в iOS/ColorOS.</summary>
    public static double RubberBand(double overshoot, double dimension, double coefficient = 0.55)
    {
        if (dimension <= 0) return 0;
        double sign = Math.Sign(overshoot);
        double x = Math.Abs(overshoot);
        return sign * (1 - 1 / (x * coefficient / dimension + 1)) * dimension;
    }

    private static double Clamp(double t) => t < 0 ? 0 : t > 1 ? 1 : t;
}
