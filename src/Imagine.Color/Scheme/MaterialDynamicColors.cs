#pragma warning disable CS8604 // роли ссылаются друг на друга лениво, к моменту вызова все поля инициализированы
namespace Imagine.Color;

/// <summary>Определения всех ролей Material 3 (спецификация 2021, как в Android 12–15).</summary>
public static class MaterialDynamicColors
{
    private static bool IsFidelity(DynamicScheme s) => s.Variant is Variant.Fidelity or Variant.Content;
    private static bool IsMonochrome(DynamicScheme s) => s.Variant == Variant.Monochrome;

    private static DynamicColor HighestSurface(DynamicScheme s) => s.IsDark ? SurfaceBright : SurfaceDim;

    private static double FindDesiredChromaByTone(double hue, double chroma, double tone, bool byDecreasingTone)
    {
        double answer = tone;
        var closest = Hct.From(hue, chroma, tone);
        if (closest.Chroma < chroma)
        {
            double chromaPeak = closest.Chroma;
            while (closest.Chroma < chroma)
            {
                answer += byDecreasingTone ? -1.0 : 1.0;
                var potential = Hct.From(hue, chroma, answer);
                if (chromaPeak > potential.Chroma) break;
                if (Math.Abs(potential.Chroma - chroma) < 0.4) break;
                if (Math.Abs(potential.Chroma - chroma) < Math.Abs(closest.Chroma - chroma)) closest = potential;
                chromaPeak = Math.Max(chromaPeak, potential.Chroma);
            }
        }
        return answer;
    }

    private static DynamicColor Background_(string name, Func<DynamicScheme, TonalPalette> palette, Func<DynamicScheme, double> tone)
        => new() { Name = name, Palette = palette, Tone = tone, IsBackground = true };

    private static ContrastCurve Curve(double l, double n, double m, double h) => new(l, n, m, h);

    // Ключевые цвета палитр
    public static readonly DynamicColor PrimaryPaletteKeyColor = new() { Name = "primaryPaletteKeyColor", Palette = s => s.PrimaryPalette, Tone = s => s.PrimaryPalette.KeyColor.Tone };
    public static readonly DynamicColor SecondaryPaletteKeyColor = new() { Name = "secondaryPaletteKeyColor", Palette = s => s.SecondaryPalette, Tone = s => s.SecondaryPalette.KeyColor.Tone };
    public static readonly DynamicColor TertiaryPaletteKeyColor = new() { Name = "tertiaryPaletteKeyColor", Palette = s => s.TertiaryPalette, Tone = s => s.TertiaryPalette.KeyColor.Tone };
    public static readonly DynamicColor NeutralPaletteKeyColor = new() { Name = "neutralPaletteKeyColor", Palette = s => s.NeutralPalette, Tone = s => s.NeutralPalette.KeyColor.Tone };
    public static readonly DynamicColor NeutralVariantPaletteKeyColor = new() { Name = "neutralVariantPaletteKeyColor", Palette = s => s.NeutralVariantPalette, Tone = s => s.NeutralVariantPalette.KeyColor.Tone };

    // Поверхности
    public static readonly DynamicColor Background = Background_("background", s => s.NeutralPalette, s => s.IsDark ? 6 : 98);
    public static readonly DynamicColor OnBackground = new()
    {
        Name = "onBackground", Palette = s => s.NeutralPalette, Tone = s => s.IsDark ? 90 : 10,
        Background = _ => Background, ContrastCurve = _ => Curve(3, 3, 4.5, 7),
    };
    public static readonly DynamicColor Surface = Background_("surface", s => s.NeutralPalette, s => s.IsDark ? 6 : 98);
    public static readonly DynamicColor SurfaceDim = Background_("surfaceDim", s => s.NeutralPalette, s => s.IsDark ? 6 : Curve(87, 87, 80, 75).Get(s.ContrastLevel));
    public static readonly DynamicColor SurfaceBright = Background_("surfaceBright", s => s.NeutralPalette, s => s.IsDark ? Curve(24, 24, 29, 34).Get(s.ContrastLevel) : 98);
    public static readonly DynamicColor SurfaceContainerLowest = Background_("surfaceContainerLowest", s => s.NeutralPalette, s => s.IsDark ? Curve(4, 4, 2, 0).Get(s.ContrastLevel) : 100);
    public static readonly DynamicColor SurfaceContainerLow = Background_("surfaceContainerLow", s => s.NeutralPalette, s => s.IsDark ? Curve(10, 10, 11, 12).Get(s.ContrastLevel) : Curve(96, 96, 96, 95).Get(s.ContrastLevel));
    public static readonly DynamicColor SurfaceContainer = Background_("surfaceContainer", s => s.NeutralPalette, s => s.IsDark ? Curve(12, 12, 16, 20).Get(s.ContrastLevel) : Curve(94, 94, 92, 90).Get(s.ContrastLevel));
    public static readonly DynamicColor SurfaceContainerHigh = Background_("surfaceContainerHigh", s => s.NeutralPalette, s => s.IsDark ? Curve(17, 17, 21, 25).Get(s.ContrastLevel) : Curve(92, 92, 88, 85).Get(s.ContrastLevel));
    public static readonly DynamicColor SurfaceContainerHighest = Background_("surfaceContainerHighest", s => s.NeutralPalette, s => s.IsDark ? Curve(22, 22, 26, 30).Get(s.ContrastLevel) : Curve(90, 90, 84, 80).Get(s.ContrastLevel));
    public static readonly DynamicColor OnSurface = new()
    {
        Name = "onSurface", Palette = s => s.NeutralPalette, Tone = s => s.IsDark ? 90 : 10,
        Background = HighestSurface, ContrastCurve = _ => Curve(4.5, 7, 11, 21),
    };
    public static readonly DynamicColor SurfaceVariant = Background_("surfaceVariant", s => s.NeutralVariantPalette, s => s.IsDark ? 30 : 90);
    public static readonly DynamicColor OnSurfaceVariant = new()
    {
        Name = "onSurfaceVariant", Palette = s => s.NeutralVariantPalette, Tone = s => s.IsDark ? 80 : 30,
        Background = HighestSurface, ContrastCurve = _ => Curve(3, 4.5, 7, 11),
    };
    public static readonly DynamicColor InverseSurface = Background_("inverseSurface", s => s.NeutralPalette, s => s.IsDark ? 90 : 20);
    public static readonly DynamicColor InverseOnSurface = new()
    {
        Name = "inverseOnSurface", Palette = s => s.NeutralPalette, Tone = s => s.IsDark ? 20 : 95,
        Background = _ => InverseSurface, ContrastCurve = _ => Curve(4.5, 7, 11, 21),
    };
    public static readonly DynamicColor Outline = new()
    {
        Name = "outline", Palette = s => s.NeutralVariantPalette, Tone = s => s.IsDark ? 60 : 50,
        Background = HighestSurface, ContrastCurve = _ => Curve(1.5, 3, 4.5, 7),
    };
    public static readonly DynamicColor OutlineVariant = new()
    {
        Name = "outlineVariant", Palette = s => s.NeutralVariantPalette, Tone = s => s.IsDark ? 30 : 80,
        Background = HighestSurface, ContrastCurve = _ => Curve(1, 1, 3, 4.5),
    };
    public static readonly DynamicColor Shadow = new() { Name = "shadow", Palette = s => s.NeutralPalette, Tone = _ => 0 };
    public static readonly DynamicColor Scrim = new() { Name = "scrim", Palette = s => s.NeutralPalette, Tone = _ => 0 };
    public static readonly DynamicColor SurfaceTint = Background_("surfaceTint", s => s.PrimaryPalette, s => s.IsDark ? 80 : 40);

    // Основной
    public static readonly DynamicColor Primary = new()
    {
        Name = "primary", Palette = s => s.PrimaryPalette,
        Tone = s => IsMonochrome(s) ? (s.IsDark ? 100 : 0) : (s.IsDark ? 80 : 40),
        IsBackground = true, Background = HighestSurface, ContrastCurve = _ => Curve(3, 4.5, 7, 7),
        ToneDeltaPair = _ => new ToneDeltaPair(PrimaryContainer, Primary, 10, TonePolarity.Nearer, false),
    };
    public static readonly DynamicColor OnPrimary = new()
    {
        Name = "onPrimary", Palette = s => s.PrimaryPalette,
        Tone = s => IsMonochrome(s) ? (s.IsDark ? 10 : 90) : (s.IsDark ? 20 : 100),
        Background = _ => Primary, ContrastCurve = _ => Curve(4.5, 7, 11, 21),
    };
    public static readonly DynamicColor PrimaryContainer = new()
    {
        Name = "primaryContainer", Palette = s => s.PrimaryPalette,
        Tone = s => IsFidelity(s) ? s.SourceColorHct.Tone : IsMonochrome(s) ? (s.IsDark ? 85 : 25) : (s.IsDark ? 30 : 90),
        IsBackground = true, Background = HighestSurface, ContrastCurve = _ => Curve(1, 1, 3, 4.5),
        ToneDeltaPair = _ => new ToneDeltaPair(PrimaryContainer, Primary, 10, TonePolarity.Nearer, false),
    };
    public static readonly DynamicColor OnPrimaryContainer = new()
    {
        Name = "onPrimaryContainer", Palette = s => s.PrimaryPalette,
        Tone = s => IsFidelity(s) ? DynamicColor.ForegroundTone(PrimaryContainer.Tone(s), 4.5) : IsMonochrome(s) ? (s.IsDark ? 0 : 100) : (s.IsDark ? 90 : 30),
        Background = _ => PrimaryContainer, ContrastCurve = _ => Curve(3, 4.5, 7, 11),
    };
    public static readonly DynamicColor InversePrimary = new()
    {
        Name = "inversePrimary", Palette = s => s.PrimaryPalette, Tone = s => s.IsDark ? 40 : 80,
        Background = _ => InverseSurface, ContrastCurve = _ => Curve(3, 4.5, 7, 7),
    };

    // Вторичный
    public static readonly DynamicColor Secondary = new()
    {
        Name = "secondary", Palette = s => s.SecondaryPalette, Tone = s => s.IsDark ? 80 : 40,
        IsBackground = true, Background = HighestSurface, ContrastCurve = _ => Curve(3, 4.5, 7, 7),
        ToneDeltaPair = _ => new ToneDeltaPair(SecondaryContainer, Secondary, 10, TonePolarity.Nearer, false),
    };
    public static readonly DynamicColor OnSecondary = new()
    {
        Name = "onSecondary", Palette = s => s.SecondaryPalette,
        Tone = s => IsMonochrome(s) ? (s.IsDark ? 10 : 100) : (s.IsDark ? 20 : 100),
        Background = _ => Secondary, ContrastCurve = _ => Curve(4.5, 7, 11, 21),
    };
    public static readonly DynamicColor SecondaryContainer = new()
    {
        Name = "secondaryContainer", Palette = s => s.SecondaryPalette,
        Tone = s => IsMonochrome(s) ? (s.IsDark ? 30 : 85)
            : IsFidelity(s) ? FindDesiredChromaByTone(s.SecondaryPalette.Hue, s.SecondaryPalette.Chroma, s.IsDark ? 30 : 90, !s.IsDark)
            : (s.IsDark ? 30 : 90),
        IsBackground = true, Background = HighestSurface, ContrastCurve = _ => Curve(1, 1, 3, 4.5),
        ToneDeltaPair = _ => new ToneDeltaPair(SecondaryContainer, Secondary, 10, TonePolarity.Nearer, false),
    };
    public static readonly DynamicColor OnSecondaryContainer = new()
    {
        Name = "onSecondaryContainer", Palette = s => s.SecondaryPalette,
        Tone = s => IsMonochrome(s) ? (s.IsDark ? 90 : 10)
            : IsFidelity(s) ? DynamicColor.ForegroundTone(SecondaryContainer.Tone(s), 4.5)
            : (s.IsDark ? 90 : 30),
        Background = _ => SecondaryContainer, ContrastCurve = _ => Curve(3, 4.5, 7, 11),
    };

    // Третичный
    public static readonly DynamicColor Tertiary = new()
    {
        Name = "tertiary", Palette = s => s.TertiaryPalette,
        Tone = s => IsMonochrome(s) ? (s.IsDark ? 90 : 25) : (s.IsDark ? 80 : 40),
        IsBackground = true, Background = HighestSurface, ContrastCurve = _ => Curve(3, 4.5, 7, 7),
        ToneDeltaPair = _ => new ToneDeltaPair(TertiaryContainer, Tertiary, 10, TonePolarity.Nearer, false),
    };
    public static readonly DynamicColor OnTertiary = new()
    {
        Name = "onTertiary", Palette = s => s.TertiaryPalette,
        Tone = s => IsMonochrome(s) ? (s.IsDark ? 10 : 90) : (s.IsDark ? 20 : 100),
        Background = _ => Tertiary, ContrastCurve = _ => Curve(4.5, 7, 11, 21),
    };
    public static readonly DynamicColor TertiaryContainer = new()
    {
        Name = "tertiaryContainer", Palette = s => s.TertiaryPalette,
        Tone = s => IsMonochrome(s) ? (s.IsDark ? 60 : 49)
            : IsFidelity(s) ? DislikeAnalyzer.FixIfDisliked(s.TertiaryPalette.GetHct(s.SourceColorHct.Tone)).Tone
            : (s.IsDark ? 30 : 90),
        IsBackground = true, Background = HighestSurface, ContrastCurve = _ => Curve(1, 1, 3, 4.5),
        ToneDeltaPair = _ => new ToneDeltaPair(TertiaryContainer, Tertiary, 10, TonePolarity.Nearer, false),
    };
    public static readonly DynamicColor OnTertiaryContainer = new()
    {
        Name = "onTertiaryContainer", Palette = s => s.TertiaryPalette,
        Tone = s => IsMonochrome(s) ? (s.IsDark ? 0 : 100)
            : IsFidelity(s) ? DynamicColor.ForegroundTone(TertiaryContainer.Tone(s), 4.5)
            : (s.IsDark ? 90 : 30),
        Background = _ => TertiaryContainer, ContrastCurve = _ => Curve(3, 4.5, 7, 11),
    };

    // Ошибка
    public static readonly DynamicColor Error = new()
    {
        Name = "error", Palette = s => s.ErrorPalette, Tone = s => s.IsDark ? 80 : 40,
        IsBackground = true, Background = HighestSurface, ContrastCurve = _ => Curve(3, 4.5, 7, 7),
        ToneDeltaPair = _ => new ToneDeltaPair(ErrorContainer, Error, 10, TonePolarity.Nearer, false),
    };
    public static readonly DynamicColor OnError = new()
    {
        Name = "onError", Palette = s => s.ErrorPalette, Tone = s => s.IsDark ? 20 : 100,
        Background = _ => Error, ContrastCurve = _ => Curve(4.5, 7, 11, 21),
    };
    public static readonly DynamicColor ErrorContainer = new()
    {
        Name = "errorContainer", Palette = s => s.ErrorPalette, Tone = s => s.IsDark ? 30 : 90,
        IsBackground = true, Background = HighestSurface, ContrastCurve = _ => Curve(1, 1, 3, 4.5),
        ToneDeltaPair = _ => new ToneDeltaPair(ErrorContainer, Error, 10, TonePolarity.Nearer, false),
    };
    public static readonly DynamicColor OnErrorContainer = new()
    {
        Name = "onErrorContainer", Palette = s => s.ErrorPalette,
        Tone = s => IsMonochrome(s) ? (s.IsDark ? 90 : 10) : (s.IsDark ? 90 : 30),
        Background = _ => ErrorContainer, ContrastCurve = _ => Curve(3, 4.5, 7, 11),
    };

    // Фиксированные (одинаковые в светлой и тёмной теме)
    public static readonly DynamicColor PrimaryFixed = Fixed("primaryFixed", s => s.PrimaryPalette, s => IsMonochrome(s) ? 40.0 : 90.0, () => PrimaryFixed!, () => PrimaryFixedDim!);
    public static readonly DynamicColor PrimaryFixedDim = Fixed("primaryFixedDim", s => s.PrimaryPalette, s => IsMonochrome(s) ? 30.0 : 80.0, () => PrimaryFixed, () => PrimaryFixedDim!);
    public static readonly DynamicColor OnPrimaryFixed = OnFixed("onPrimaryFixed", s => s.PrimaryPalette, s => IsMonochrome(s) ? 100.0 : 10.0, () => PrimaryFixedDim, () => PrimaryFixed, Curve(4.5, 7, 11, 21));
    public static readonly DynamicColor OnPrimaryFixedVariant = OnFixed("onPrimaryFixedVariant", s => s.PrimaryPalette, s => IsMonochrome(s) ? 90.0 : 30.0, () => PrimaryFixedDim, () => PrimaryFixed, Curve(3, 4.5, 7, 11));
    public static readonly DynamicColor SecondaryFixed = Fixed("secondaryFixed", s => s.SecondaryPalette, s => IsMonochrome(s) ? 80.0 : 90.0, () => SecondaryFixed!, () => SecondaryFixedDim!);
    public static readonly DynamicColor SecondaryFixedDim = Fixed("secondaryFixedDim", s => s.SecondaryPalette, s => IsMonochrome(s) ? 70.0 : 80.0, () => SecondaryFixed, () => SecondaryFixedDim!);
    public static readonly DynamicColor OnSecondaryFixed = OnFixed("onSecondaryFixed", s => s.SecondaryPalette, _ => 10.0, () => SecondaryFixedDim, () => SecondaryFixed, Curve(4.5, 7, 11, 21));
    public static readonly DynamicColor OnSecondaryFixedVariant = OnFixed("onSecondaryFixedVariant", s => s.SecondaryPalette, s => IsMonochrome(s) ? 25.0 : 30.0, () => SecondaryFixedDim, () => SecondaryFixed, Curve(3, 4.5, 7, 11));
    public static readonly DynamicColor TertiaryFixed = Fixed("tertiaryFixed", s => s.TertiaryPalette, s => IsMonochrome(s) ? 40.0 : 90.0, () => TertiaryFixed!, () => TertiaryFixedDim!);
    public static readonly DynamicColor TertiaryFixedDim = Fixed("tertiaryFixedDim", s => s.TertiaryPalette, s => IsMonochrome(s) ? 30.0 : 80.0, () => TertiaryFixed, () => TertiaryFixedDim!);
    public static readonly DynamicColor OnTertiaryFixed = OnFixed("onTertiaryFixed", s => s.TertiaryPalette, s => IsMonochrome(s) ? 100.0 : 10.0, () => TertiaryFixedDim, () => TertiaryFixed, Curve(4.5, 7, 11, 21));
    public static readonly DynamicColor OnTertiaryFixedVariant = OnFixed("onTertiaryFixedVariant", s => s.TertiaryPalette, s => IsMonochrome(s) ? 90.0 : 30.0, () => TertiaryFixedDim, () => TertiaryFixed, Curve(3, 4.5, 7, 11));

    private static DynamicColor Fixed(string name, Func<DynamicScheme, TonalPalette> palette, Func<DynamicScheme, double> tone, Func<DynamicColor> a, Func<DynamicColor> b) => new()
    {
        Name = name, Palette = palette, Tone = tone, IsBackground = true,
        Background = HighestSurface, ContrastCurve = _ => Curve(1, 1, 3, 4.5),
        ToneDeltaPair = _ => new ToneDeltaPair(a(), b(), 10, TonePolarity.Lighter, true),
    };

    private static DynamicColor OnFixed(string name, Func<DynamicScheme, TonalPalette> palette, Func<DynamicScheme, double> tone, Func<DynamicColor> bg, Func<DynamicColor> bg2, ContrastCurve curve) => new()
    {
        Name = name, Palette = palette, Tone = tone,
        Background = _ => bg(), SecondBackground = _ => bg2(), ContrastCurve = _ => curve,
    };

    private static readonly Lazy<Dictionary<ColorRole, DynamicColor>> ByRole = new(() => new()
    {
        [ColorRole.PrimaryPaletteKeyColor] = PrimaryPaletteKeyColor,
        [ColorRole.SecondaryPaletteKeyColor] = SecondaryPaletteKeyColor,
        [ColorRole.TertiaryPaletteKeyColor] = TertiaryPaletteKeyColor,
        [ColorRole.NeutralPaletteKeyColor] = NeutralPaletteKeyColor,
        [ColorRole.NeutralVariantPaletteKeyColor] = NeutralVariantPaletteKeyColor,
        [ColorRole.Background] = Background,
        [ColorRole.OnBackground] = OnBackground,
        [ColorRole.Surface] = Surface,
        [ColorRole.SurfaceDim] = SurfaceDim,
        [ColorRole.SurfaceBright] = SurfaceBright,
        [ColorRole.SurfaceContainerLowest] = SurfaceContainerLowest,
        [ColorRole.SurfaceContainerLow] = SurfaceContainerLow,
        [ColorRole.SurfaceContainer] = SurfaceContainer,
        [ColorRole.SurfaceContainerHigh] = SurfaceContainerHigh,
        [ColorRole.SurfaceContainerHighest] = SurfaceContainerHighest,
        [ColorRole.OnSurface] = OnSurface,
        [ColorRole.SurfaceVariant] = SurfaceVariant,
        [ColorRole.OnSurfaceVariant] = OnSurfaceVariant,
        [ColorRole.InverseSurface] = InverseSurface,
        [ColorRole.InverseOnSurface] = InverseOnSurface,
        [ColorRole.Outline] = Outline,
        [ColorRole.OutlineVariant] = OutlineVariant,
        [ColorRole.Shadow] = Shadow,
        [ColorRole.Scrim] = Scrim,
        [ColorRole.SurfaceTint] = SurfaceTint,
        [ColorRole.Primary] = Primary,
        [ColorRole.OnPrimary] = OnPrimary,
        [ColorRole.PrimaryContainer] = PrimaryContainer,
        [ColorRole.OnPrimaryContainer] = OnPrimaryContainer,
        [ColorRole.InversePrimary] = InversePrimary,
        [ColorRole.Secondary] = Secondary,
        [ColorRole.OnSecondary] = OnSecondary,
        [ColorRole.SecondaryContainer] = SecondaryContainer,
        [ColorRole.OnSecondaryContainer] = OnSecondaryContainer,
        [ColorRole.Tertiary] = Tertiary,
        [ColorRole.OnTertiary] = OnTertiary,
        [ColorRole.TertiaryContainer] = TertiaryContainer,
        [ColorRole.OnTertiaryContainer] = OnTertiaryContainer,
        [ColorRole.Error] = Error,
        [ColorRole.OnError] = OnError,
        [ColorRole.ErrorContainer] = ErrorContainer,
        [ColorRole.OnErrorContainer] = OnErrorContainer,
        [ColorRole.PrimaryFixed] = PrimaryFixed,
        [ColorRole.PrimaryFixedDim] = PrimaryFixedDim,
        [ColorRole.OnPrimaryFixed] = OnPrimaryFixed,
        [ColorRole.OnPrimaryFixedVariant] = OnPrimaryFixedVariant,
        [ColorRole.SecondaryFixed] = SecondaryFixed,
        [ColorRole.SecondaryFixedDim] = SecondaryFixedDim,
        [ColorRole.OnSecondaryFixed] = OnSecondaryFixed,
        [ColorRole.OnSecondaryFixedVariant] = OnSecondaryFixedVariant,
        [ColorRole.TertiaryFixed] = TertiaryFixed,
        [ColorRole.TertiaryFixedDim] = TertiaryFixedDim,
        [ColorRole.OnTertiaryFixed] = OnTertiaryFixed,
        [ColorRole.OnTertiaryFixedVariant] = OnTertiaryFixedVariant,
    });

    public static DynamicColor Get(ColorRole role) => ByRole.Value[role];
}
