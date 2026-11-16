namespace Imagine.Core.Theming;

/// <summary>
/// Восход и закат по алгоритму NOAA (точность ~1–2 минуты для широт до ±65°).
/// Нужен, чтобы палитра «дышала» вместе с реальным днём у пользователя.
/// </summary>
public static class SunCalculator
{
    /// <summary>Возвращает восход и закат в UTC. null — полярный день или ночь.</summary>
    public static (DateTimeOffset Sunrise, DateTimeOffset Sunset)? GetSunTimes(DateOnly date, double latitude, double longitude)
    {
        double sunrise = CalcUtc(date, latitude, longitude, true);
        double sunset = CalcUtc(date, latitude, longitude, false);
        if (double.IsNaN(sunrise) || double.IsNaN(sunset)) return null;
        var midnight = new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero);
        return (midnight.AddMinutes(sunrise), midnight.AddMinutes(sunset));
    }

    private static double CalcUtc(DateOnly date, double lat, double lng, bool rising)
    {
        double jd = JulianDay(date);
        double t = (jd - 2451545.0) / 36525.0;
        // Два прохода уточнения: сначала по полудню, затем по найденному времени.
        double noon = SolarNoonUtc(t, lng);
        double tNoon = (jd + noon / 1440.0 - 2451545.0) / 36525.0;
        double eqTime = EquationOfTime(tNoon);
        double decl = SunDeclination(tNoon);
        double ha = HourAngle(lat, decl, rising);
        if (double.IsNaN(ha)) return double.NaN;
        double delta = lng + RadToDeg(ha);
        double timeUtc = 720 - 4.0 * delta - eqTime;

        double tNew = (jd + timeUtc / 1440.0 - 2451545.0) / 36525.0;
        eqTime = EquationOfTime(tNew);
        decl = SunDeclination(tNew);
        ha = HourAngle(lat, decl, rising);
        if (double.IsNaN(ha)) return double.NaN;
        delta = lng + RadToDeg(ha);
        return 720 - 4.0 * delta - eqTime;
    }

    private static double JulianDay(DateOnly d)
    {
        int year = d.Year, month = d.Month;
        if (month <= 2) { year -= 1; month += 12; }
        double a = Math.Floor(year / 100.0);
        double b = 2 - a + Math.Floor(a / 4);
        return Math.Floor(365.25 * (year + 4716)) + Math.Floor(30.6001 * (month + 1)) + d.Day + b - 1524.5;
    }

    private static double SolarNoonUtc(double t, double longitude)
    {
        double tnoon = t + (-longitude / 360.0) / 36525.0;
        double eqTime = EquationOfTime(tnoon);
        return 720 - longitude * 4 - eqTime;
    }

    private static double DegToRad(double a) => Math.PI * a / 180.0;
    private static double RadToDeg(double a) => 180.0 * a / Math.PI;

    private static double GeomMeanLongSun(double t)
    {
        double l0 = 280.46646 + t * (36000.76983 + 0.0003032 * t);
        l0 %= 360;
        return l0 < 0 ? l0 + 360 : l0;
    }

    private static double GeomMeanAnomalySun(double t) => 357.52911 + t * (35999.05029 - 0.0001537 * t);

    private static double EccentricityEarthOrbit(double t) => 0.016708634 - t * (0.000042037 + 0.0000001267 * t);

    private static double SunEqOfCenter(double t)
    {
        double m = DegToRad(GeomMeanAnomalySun(t));
        return Math.Sin(m) * (1.914602 - t * (0.004817 + 0.000014 * t)) + Math.Sin(2 * m) * (0.019993 - 0.000101 * t) + Math.Sin(3 * m) * 0.000289;
    }

    private static double SunApparentLong(double t)
    {
        double trueLong = GeomMeanLongSun(t) + SunEqOfCenter(t);
        double omega = 125.04 - 1934.136 * t;
        return trueLong - 0.00569 - 0.00478 * Math.Sin(DegToRad(omega));
    }

    private static double MeanObliquityOfEcliptic(double t)
    {
        double seconds = 21.448 - t * (46.8150 + t * (0.00059 - t * 0.001813));
        return 23.0 + (26.0 + seconds / 60.0) / 60.0;
    }

    private static double ObliquityCorrection(double t)
    {
        double omega = 125.04 - 1934.136 * t;
        return MeanObliquityOfEcliptic(t) + 0.00256 * Math.Cos(DegToRad(omega));
    }

    private static double SunDeclination(double t)
    {
        double e = ObliquityCorrection(t);
        double lambda = SunApparentLong(t);
        return RadToDeg(Math.Asin(Math.Sin(DegToRad(e)) * Math.Sin(DegToRad(lambda))));
    }

    private static double EquationOfTime(double t)
    {
        double epsilon = ObliquityCorrection(t);
        double l0 = GeomMeanLongSun(t);
        double e = EccentricityEarthOrbit(t);
        double m = GeomMeanAnomalySun(t);
        double y = Math.Tan(DegToRad(epsilon) / 2.0);
        y *= y;
        double sin2l0 = Math.Sin(2.0 * DegToRad(l0));
        double sinm = Math.Sin(DegToRad(m));
        double cos2l0 = Math.Cos(2.0 * DegToRad(l0));
        double sin4l0 = Math.Sin(4.0 * DegToRad(l0));
        double sin2m = Math.Sin(2.0 * DegToRad(m));
        double eTime = y * sin2l0 - 2.0 * e * sinm + 4.0 * e * y * sinm * cos2l0 - 0.5 * y * y * sin4l0 - 1.25 * e * e * sin2m;
        return RadToDeg(eTime) * 4.0;
    }

    private static double HourAngle(double lat, double solarDec, bool rising)
    {
        double latRad = DegToRad(lat);
        double sdRad = DegToRad(solarDec);
        double cosHa = Math.Cos(DegToRad(90.833)) / (Math.Cos(latRad) * Math.Cos(sdRad)) - Math.Tan(latRad) * Math.Tan(sdRad);
        if (cosHa < -1 || cosHa > 1) return double.NaN;
        double ha = Math.Acos(cosHa);
        return rising ? ha : -ha;
    }
}
