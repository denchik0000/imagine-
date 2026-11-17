namespace Imagine.Color;

/// <summary>Числовые утилиты, перенесённые из material-color-utilities.</summary>
public static class MathUtils
{
    public static int Signum(double num) => num < 0 ? -1 : num == 0 ? 0 : 1;

    public static double Lerp(double start, double stop, double amount) => (1.0 - amount) * start + amount * stop;

    public static int ClampInt(int min, int max, int input) => input < min ? min : input > max ? max : input;

    public static double ClampDouble(double min, double max, double input) => input < min ? min : input > max ? max : input;

    public static int SanitizeDegreesInt(int degrees)
    {
        degrees %= 360;
        if (degrees < 0) degrees += 360;
        return degrees;
    }

    public static double SanitizeDegreesDouble(double degrees)
    {
        degrees %= 360.0;
        if (degrees < 0) degrees += 360.0;
        return degrees;
    }

    public static double RotationDirection(double from, double to)
        => SanitizeDegreesDouble(to - from) <= 180.0 ? 1.0 : -1.0;

    public static double DifferenceDegrees(double a, double b) => 180.0 - Math.Abs(Math.Abs(a - b) - 180.0);

    public static double[] MatrixMultiply(double[] row, double[,] m) =>
    [
        row[0] * m[0, 0] + row[1] * m[0, 1] + row[2] * m[0, 2],
        row[0] * m[1, 0] + row[1] * m[1, 1] + row[2] * m[1, 2],
        row[0] * m[2, 0] + row[1] * m[2, 1] + row[2] * m[2, 2],
    ];

    /// <summary>Округление как Math.round в Java (половина вверх).</summary>
    public static int RoundHalfUp(double value) => (int)Math.Floor(value + 0.5);
}
