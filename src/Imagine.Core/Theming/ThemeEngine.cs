using System.Collections.Frozen;
using Imagine.Color;

namespace Imagine.Core.Theming;

/// <summary>Входные данные для генерации темы.</summary>
public sealed record ThemeRequest
{
    public WallpaperAnalysis? Wallpaper { get; init; }
    public ThemeOptions Options { get; init; } = new();
    public GlassStyle Glass { get; init; } = new();
    public DaySchedule Schedule { get; init; } = new();
    public DateTimeOffset Now { get; init; } = DateTimeOffset.Now;
    /// <summary>Затравка из обложки играющего трека, если есть.</summary>
    public uint? NowPlayingSeed { get; init; }
}

/// <summary>Превращает обои и настройки в готовую тему.</summary>
public static class ThemeEngine
{
    /// <summary>Самый «тёплый» оттенок HCT, к которому палитра тянется вечером.</summary>
    public const double WarmHue = 45;

    public static ThemePalette Generate(ThemeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var o = request.Options;
        var phase = request.Schedule.PhaseAt(request.Now);

        uint seed = ResolveSeed(request);
        if (o.EveningWarmShift > 0 && phase is DayPhase.GoldenHour or DayPhase.Dusk or DayPhase.Night)
        {
            double strength = phase == DayPhase.GoldenHour ? 1.0 : 0.6;
            seed = ShiftTowardsWarm(seed, o.EveningWarmShift * strength);
        }

        bool isDark = o.Mode switch
        {
            ThemeModePreference.Light => false,
            ThemeModePreference.Dark => true,
            ThemeModePreference.BySchedule => DaySchedule.IsDarkPhase(phase),
            _ => request.Wallpaper?.PrefersDarkTheme ?? false,
        };

        var hct = Hct.FromArgb(seed);
        var scheme = new DynamicScheme(hct, o.Variant, isDark, o.ContrastLevel);
        var opposite = new DynamicScheme(hct, o.Variant, !isDark, o.ContrastLevel);
        return new ThemePalette
        {
            Seed = seed,
            Variant = o.Variant,
            IsDark = isDark,
            ContrastLevel = o.ContrastLevel,
            Scheme = scheme,
            OppositeScheme = opposite,
            Roles = scheme.ToDictionary().ToFrozenDictionary(),
            Accent = WindowsAccentPalette.FromScheme(scheme),
            Glass = GlassTokens.From(scheme, request.Glass),
        };
    }

    public static uint ResolveSeed(ThemeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var o = request.Options;
        uint wallpaperSeed = Score.GoogleBlue;
        if (request.Wallpaper is { SeedCandidates.Count: > 0 } wp)
            wallpaperSeed = wp.SeedCandidates[Math.Clamp(o.WallpaperSeedIndex, 0, wp.SeedCandidates.Count - 1)];

        return o.SeedSource switch
        {
            SeedSource.Custom when o.CustomSeed is { } custom => custom | 0xFF000000,
            SeedSource.NowPlaying when request.NowPlayingSeed is { } art => Blend.Cam16Ucs(wallpaperSeed, art, MathUtils.ClampDouble(0, 1, o.NowPlayingBlend)),
            _ => wallpaperSeed,
        };
    }

    /// <summary>Поворачивает оттенок к тёплому не более чем на <paramref name="degrees"/>.</summary>
    public static uint ShiftTowardsWarm(uint argb, double degrees)
    {
        var hct = Hct.FromArgb(argb);
        if (hct.Chroma < 4) return argb; // серый остаётся серым
        double diff = MathUtils.DifferenceDegrees(hct.Hue, WarmHue);
        double step = Math.Min(diff, degrees);
        double hue = MathUtils.SanitizeDegreesDouble(hct.Hue + step * MathUtils.RotationDirection(hct.Hue, WarmHue));
        return Hct.From(hue, hct.Chroma, hct.Tone).Argb;
    }

    /// <summary>Все стили схем для превью-карточек выбора (как «Стили» в ColorOS / Android).</summary>
    public static IReadOnlyList<(Variant Variant, DynamicScheme Scheme)> PreviewVariants(uint seed, bool isDark)
        => Enum.GetValues<Variant>().Select(v => (v, new DynamicScheme(seed, v, isDark))).ToList();
}
