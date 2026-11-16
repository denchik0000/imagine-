namespace Imagine.Color;

/// <summary>
/// Роль цвета в схеме (primary, surface…): знает свою палитру, базовый тон и
/// фон, относительно которого нужно выдержать контраст. Алгоритм — спецификация Material 2021.
/// </summary>
public sealed class DynamicColor
{
    public required string Name { get; init; }
    public required Func<DynamicScheme, TonalPalette> Palette { get; init; }
    public required Func<DynamicScheme, double> Tone { get; init; }
    public bool IsBackground { get; init; }
    public Func<DynamicScheme, DynamicColor?>? Background { get; init; }
    public Func<DynamicScheme, DynamicColor?>? SecondBackground { get; init; }
    public Func<DynamicScheme, ContrastCurve>? ContrastCurve { get; init; }
    public Func<DynamicScheme, ToneDeltaPair?>? ToneDeltaPair { get; init; }

    public uint GetArgb(DynamicScheme scheme) => Palette(scheme).Tone(GetTone(scheme));

    public Hct GetHct(DynamicScheme scheme) => Hct.FromArgb(GetArgb(scheme));

    public double GetTone(DynamicScheme scheme)
    {
        bool decreasingContrast = scheme.ContrastLevel < 0;
        var pair = ToneDeltaPair?.Invoke(scheme);
        if (pair is not null)
        {
            var roleA = pair.RoleA;
            var roleB = pair.RoleB;
            double delta = pair.Delta;
            bool aIsNearer = pair.Polarity == TonePolarity.Nearer
                             || (pair.Polarity == TonePolarity.Lighter && !scheme.IsDark)
                             || (pair.Polarity == TonePolarity.Darker && scheme.IsDark);
            var nearer = aIsNearer ? roleA : roleB;
            var farther = aIsNearer ? roleB : roleA;
            bool amNearer = Name == nearer.Name;
            double expansionDir = scheme.IsDark ? 1 : -1;
            double nTone = nearer.Tone(scheme);
            double fTone = farther.Tone(scheme);

            var bg = Background?.Invoke(scheme);
            if (bg is not null && nearer.ContrastCurve is not null && farther.ContrastCurve is not null)
            {
                double bgTone = bg.GetTone(scheme);
                double nContrast = nearer.ContrastCurve(scheme).Get(scheme.ContrastLevel);
                double fContrast = farther.ContrastCurve(scheme).Get(scheme.ContrastLevel);
                if (Contrast.RatioOfTones(bgTone, nTone) < nContrast) nTone = ForegroundTone(bgTone, nContrast);
                if (Contrast.RatioOfTones(bgTone, fTone) < fContrast) fTone = ForegroundTone(bgTone, fContrast);
                if (decreasingContrast)
                {
                    nTone = ForegroundTone(bgTone, nContrast);
                    fTone = ForegroundTone(bgTone, fContrast);
                }
            }

            if ((fTone - nTone) * expansionDir < delta)
            {
                fTone = MathUtils.ClampDouble(0, 100, nTone + delta * expansionDir);
                if ((fTone - nTone) * expansionDir < delta)
                    nTone = MathUtils.ClampDouble(0, 100, fTone - delta * expansionDir);
            }

            if (nTone >= 50 && nTone < 60)
            {
                if (expansionDir > 0) { nTone = 60; fTone = Math.Max(fTone, nTone + delta * expansionDir); }
                else { nTone = 49; fTone = Math.Min(fTone, nTone + delta * expansionDir); }
            }
            else if (fTone >= 50 && fTone < 60)
            {
                if (pair.StayTogether)
                {
                    if (expansionDir > 0) { nTone = 60; fTone = Math.Max(fTone, nTone + delta * expansionDir); }
                    else { nTone = 49; fTone = Math.Min(fTone, nTone + delta * expansionDir); }
                }
                else
                {
                    fTone = expansionDir > 0 ? 60 : 49;
                }
            }
            return amNearer ? nTone : fTone;
        }

        double answer = Tone(scheme);
        var background = Background?.Invoke(scheme);
        var curve = ContrastCurve?.Invoke(scheme);
        if (background is null || curve is null) return answer;

        double bgTone1 = background.GetTone(scheme);
        double desiredRatio = curve.Get(scheme.ContrastLevel);
        if (Contrast.RatioOfTones(bgTone1, answer) < desiredRatio) answer = ForegroundTone(bgTone1, desiredRatio);
        if (decreasingContrast) answer = ForegroundTone(bgTone1, desiredRatio);
        if (IsBackground && answer >= 50 && answer < 60)
            answer = Contrast.RatioOfTones(49, bgTone1) >= desiredRatio ? 49 : 60;

        var second = SecondBackground?.Invoke(scheme);
        if (second is null) return answer;

        double bgToneA = background.GetTone(scheme);
        double bgToneB = second.GetTone(scheme);
        double upper = Math.Max(bgToneA, bgToneB);
        double lower = Math.Min(bgToneA, bgToneB);
        if (Contrast.RatioOfTones(upper, answer) >= desiredRatio && Contrast.RatioOfTones(lower, answer) >= desiredRatio)
            return answer;

        double lightOption = Contrast.Lighter(upper, desiredRatio);
        double darkOption = Contrast.Darker(lower, desiredRatio);
        int available = (lightOption != -1 ? 1 : 0) + (darkOption != -1 ? 1 : 0);
        bool prefersLight = TonePrefersLightForeground(bgToneA) || TonePrefersLightForeground(bgToneB);
        if (prefersLight) return lightOption < 0 ? 100 : lightOption;
        if (available == 1) return lightOption != -1 ? lightOption : darkOption;
        return darkOption < 0 ? 0 : darkOption;
    }

    /// <summary>Тон текста на фоне <paramref name="bgTone"/> с контрастом не ниже <paramref name="ratio"/>.</summary>
    public static double ForegroundTone(double bgTone, double ratio)
    {
        double lighterTone = Contrast.LighterUnsafe(bgTone, ratio);
        double darkerTone = Contrast.DarkerUnsafe(bgTone, ratio);
        double lighterRatio = Contrast.RatioOfTones(lighterTone, bgTone);
        double darkerRatio = Contrast.RatioOfTones(darkerTone, bgTone);
        if (TonePrefersLightForeground(bgTone))
        {
            bool negligibleDifference = Math.Abs(lighterRatio - darkerRatio) < 0.1 && lighterRatio < ratio && darkerRatio < ratio;
            return lighterRatio >= ratio || lighterRatio >= darkerRatio || negligibleDifference ? lighterTone : darkerTone;
        }
        return darkerRatio >= ratio || darkerRatio >= lighterRatio ? darkerTone : lighterTone;
    }

    public static bool TonePrefersLightForeground(double tone) => Math.Round(tone) < 60.0;

    public static bool ToneAllowsLightForeground(double tone) => Math.Round(tone) <= 49.0;

    public static double EnableLightForeground(double tone)
        => TonePrefersLightForeground(tone) && !ToneAllowsLightForeground(tone) ? 49.0 : tone;

    public override string ToString() => Name;
}
