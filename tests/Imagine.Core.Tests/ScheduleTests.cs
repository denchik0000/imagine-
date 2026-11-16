using Imagine.Core.Theming;

namespace Imagine.Core.Tests;

public class ScheduleTests
{
    [Fact]
    public void Moscow_summer_solstice_sun_times_are_close_to_almanac()
    {
        var sun = SunCalculator.GetSunTimes(new DateOnly(2026, 6, 21), 55.7558, 37.6173);
        Assert.NotNull(sun);
        var msk = TimeSpan.FromHours(3);
        var rise = sun.Value.Sunrise.ToOffset(msk);
        var set = sun.Value.Sunset.ToOffset(msk);
        // Справочник: восход ~03:44, закат ~21:18 по Москве.
        Assert.InRange(rise.TimeOfDay.TotalMinutes, 3 * 60 + 38, 3 * 60 + 50);
        Assert.InRange(set.TimeOfDay.TotalMinutes, 21 * 60 + 12, 21 * 60 + 24);
    }

    [Fact]
    public void Polar_night_returns_null()
    {
        Assert.Null(SunCalculator.GetSunTimes(new DateOnly(2026, 12, 21), 78.2, 15.6));
    }

    [Theory]
    [InlineData(3, DayPhase.Night)]
    [InlineData(7, DayPhase.Dawn)]
    [InlineData(12, DayPhase.Day)]
    [InlineData(19, DayPhase.GoldenHour)]
    [InlineData(20, DayPhase.Dusk)]
    [InlineData(23, DayPhase.Night)]
    public void Fixed_schedule_phases(int hour, DayPhase expected)
    {
        var s = new DaySchedule { UseSun = false, LightFrom = new TimeOnly(7, 0), DarkFrom = new TimeOnly(20, 0) };
        var now = new DateTimeOffset(2026, 11, 16, hour, hour == 20 ? 20 : 0, 0, TimeSpan.FromHours(3));
        Assert.Equal(expected, s.PhaseAt(now));
    }
}
