namespace Imagine.Core.Clock;

/// <summary>Положение и размер часов в пикселях.</summary>
public readonly record struct ClockFrame(double X, double Y, double FontSize, double Width, double Height);

/// <summary>
/// «Жидкие» часы: размер и место зависят от экрана, числа уведомлений и того, где на обоях пусто.
/// Одна и та же раскладка используется для экрана блокировки и рабочего стола, поэтому при входе
/// в систему часы визуально «переезжают», а не появляются заново.
/// </summary>
public static class FluidClockLayout
{
    public static ClockFrame Compute(ClockStyle style, double screenWidth, double screenHeight, int notificationCount = 0, double? emptyAreaCenterX = null)
    {
        ArgumentNullException.ThrowIfNull(style);
        double baseSize = screenHeight * Math.Clamp(style.RelativeSize, 0.05, 0.4);
        // Чем больше уведомлений, тем компактнее часы (как в ColorOS 17), но не меньше 55%.
        double shrink = Math.Max(0.55, 1 - 0.08 * Math.Min(notificationCount, 6));
        double fontSize = baseSize * shrink;
        double lines = style.Layout == ClockLayout.Stacked ? 2 : 1;
        double digitWidth = fontSize * 0.58;
        double width = style.Layout switch
        {
            ClockLayout.Stacked => digitWidth * 2.2,
            ClockLayout.Analog => fontSize * 1.6,
            _ => digitWidth * (style.ShowSeconds ? 8 : 5.2),
        };
        double height = style.Layout == ClockLayout.Analog ? width : fontSize * lines * 0.92 + (style.ShowDate ? fontSize * 0.28 : 0);

        double margin = Math.Min(screenWidth, screenHeight) * 0.05;
        double x, y;
        switch (style.Placement)
        {
            case ClockPlacement.TopCenter: x = (screenWidth - width) / 2; y = margin; break;
            case ClockPlacement.TopRight: x = screenWidth - width - margin; y = margin; break;
            case ClockPlacement.Center: x = (screenWidth - width) / 2; y = (screenHeight - height) / 2; break;
            case ClockPlacement.BottomLeft: x = margin; y = screenHeight - height - margin * 1.6; break;
            case ClockPlacement.BottomRight: x = screenWidth - width - margin; y = screenHeight - height - margin * 1.6; break;
            case ClockPlacement.Custom: x = style.CustomX * screenWidth; y = style.CustomY * screenHeight; break;
            default: x = margin; y = margin; break;
        }
        // Если анализ обоев нашёл пустую область, мягко смещаемся к ней по горизонтали (только для автоматических позиций).
        if (emptyAreaCenterX is { } cx && style.Placement is ClockPlacement.TopLeft or ClockPlacement.TopRight or ClockPlacement.TopCenter)
            x = Math.Clamp(cx * screenWidth - width / 2, margin, screenWidth - width - margin);
        return new ClockFrame(x, y, fontSize, width, height);
    }

    /// <summary>Промежуточная рамка для анимации переезда (t 0..1).</summary>
    public static ClockFrame Interpolate(ClockFrame from, ClockFrame to, double t)
    {
        t = Math.Clamp(t, 0, 1);
        double L(double a, double b) => a + (b - a) * t;
        return new ClockFrame(L(from.X, to.X), L(from.Y, to.Y), L(from.FontSize, to.FontSize), L(from.Width, to.Width), L(from.Height, to.Height));
    }
}
