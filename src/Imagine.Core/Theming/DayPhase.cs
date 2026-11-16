namespace Imagine.Core.Theming;

/// <summary>Часть суток, от которой зависит «дыхание» палитры.</summary>
public enum DayPhase
{
    Night,
    /// <summary>Час вокруг восхода.</summary>
    Dawn,
    Day,
    /// <summary>«Золотой час» перед закатом.</summary>
    GoldenHour,
    /// <summary>Сумерки после заката.</summary>
    Dusk,
}

/// <summary>Расписание: по солнцу (нужны координаты) или по фиксированным часам.</summary>
public sealed record DaySchedule
{
    public bool UseSun { get; init; }
    public double Latitude { get; init; } = 55.75;
    public double Longitude { get; init; } = 37.62;
    public TimeOnly LightFrom { get; init; } = new(7, 0);
    public TimeOnly DarkFrom { get; init; } = new(20, 0);

    /// <summary>Определяет фазу для момента <paramref name="now"/> (локальное время с часовым поясом).</summary>
    public DayPhase PhaseAt(DateTimeOffset now)
    {
        DateTimeOffset sunrise, sunset;
        if (UseSun && SunCalculator.GetSunTimes(DateOnly.FromDateTime(now.Date), Latitude, Longitude) is { } sun)
        {
            sunrise = sun.Sunrise.ToOffset(now.Offset);
            sunset = sun.Sunset.ToOffset(now.Offset);
        }
        else if (UseSun)
        {
            // Полярный день или ночь: решаем по месяцу и полушарию.
            bool summer = (now.Month is >= 4 and <= 9) == (Latitude >= 0);
            return summer ? DayPhase.Day : DayPhase.Night;
        }
        else
        {
            var date = now.Date;
            sunrise = new DateTimeOffset(date + LightFrom.ToTimeSpan(), now.Offset);
            sunset = new DateTimeOffset(date + DarkFrom.ToTimeSpan(), now.Offset);
        }

        var half = TimeSpan.FromMinutes(30);
        if (now < sunrise - half) return DayPhase.Night;
        if (now < sunrise + half) return DayPhase.Dawn;
        if (now < sunset - TimeSpan.FromHours(1)) return DayPhase.Day;
        if (now < sunset) return DayPhase.GoldenHour;
        if (now < sunset + TimeSpan.FromMinutes(45)) return DayPhase.Dusk;
        return DayPhase.Night;
    }

    public static bool IsDarkPhase(DayPhase phase) => phase is DayPhase.Night or DayPhase.Dusk;
}
