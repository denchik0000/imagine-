using System.Globalization;

namespace Imagine.Core.Clock;

/// <summary>Тексты часов и даты.</summary>
public static class ClockFormatter
{
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    public static (string Hours, string Minutes, string? Seconds, string? AmPm) Digits(DateTime time, ClockStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        string hours = style.Use24Hour
            ? time.ToString("HH", CultureInfo.InvariantCulture)
            : (time.Hour % 12 == 0 ? 12 : time.Hour % 12).ToString(CultureInfo.InvariantCulture);
        string minutes = time.ToString("mm", CultureInfo.InvariantCulture);
        string? seconds = style.ShowSeconds ? time.ToString("ss", CultureInfo.InvariantCulture) : null;
        string? ampm = style.Use24Hour ? null : time.Hour < 12 ? "AM" : "PM";
        return (hours, minutes, seconds, ampm);
    }

    /// <summary>«Понедельник, 16 ноября».</summary>
    public static string LongDate(DateTime date, CultureInfo? culture = null)
    {
        var c = culture ?? Ru;
        string s = date.ToString("dddd, d MMMM", c);
        return char.ToUpper(s[0], c) + s[1..];
    }

    /// <summary>«Пн, 16 нояб.».</summary>
    public static string ShortDate(DateTime date, CultureInfo? culture = null)
    {
        var c = culture ?? Ru;
        string s = date.ToString("ddd, d MMM", c);
        return char.ToUpper(s[0], c) + s[1..];
    }

    /// <summary>Угол стрелок аналоговых часов в градусах (0 — вверх, по часовой).</summary>
    public static (double Hour, double Minute, double Second) HandAngles(DateTime time)
    {
        double s = time.Second + time.Millisecond / 1000.0;
        double m = time.Minute + s / 60.0;
        double h = time.Hour % 12 + m / 60.0;
        return (h * 30, m * 6, s * 6);
    }
}
