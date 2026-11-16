using Imagine.Color;
using Imagine.Core.Exporters;
using Imagine.Core.Theming;

namespace Imagine.Core.Tests;

public class ThemingTests
{
    internal static PixelBuffer Stripes(int w, int h, params uint[] colors)
    {
        var px = new uint[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = colors[x * colors.Length / w];
        return new PixelBuffer(w, h, px);
    }

    [Fact]
    public void Analyzer_picks_dominant_saturated_color_as_seed()
    {
        // 70% кораллового, 30% серого.
        var img = Stripes(200, 100, 0xFFD55E5D, 0xFFD55E5D, 0xFFD55E5D, 0xFFD55E5D, 0xFFD55E5D, 0xFFD55E5D, 0xFFD55E5D, 0xFF808080, 0xFF808080, 0xFF808080);
        var a = WallpaperAnalyzer.Analyze(img);
        Assert.True(Cam16.FromArgb(a.Seed).Distance(Cam16.FromArgb(0xFFD55E5D)) < 3);
        Assert.InRange(a.Colorfulness, 0.6, 0.8);
        Assert.Equal(200, a.Width);
    }

    [Fact]
    public void Dark_wallpaper_prefers_dark_theme()
    {
        var a = WallpaperAnalyzer.Analyze(Stripes(64, 64, 0xFF101828, 0xFF1B2B44));
        Assert.True(a.PrefersDarkTheme);
        var light = WallpaperAnalyzer.Analyze(Stripes(64, 64, 0xFFF4E7E7, 0xFFFFFFFF));
        Assert.False(light.PrefersDarkTheme);
        Assert.True(light.TopIsLight);
    }

    [Fact]
    public void Portrait_photo_on_landscape_screen_suggests_rotation()
    {
        Assert.True(WallpaperAnalyzer.SuggestsRotation(1152, 2048, 1920, 1080));
        Assert.False(WallpaperAnalyzer.SuggestsRotation(1920, 1080, 1920, 1080));
        Assert.False(WallpaperAnalyzer.SuggestsRotation(1000, 1000, 1920, 1080));
    }

    [Fact]
    public void Pixel_buffer_rotations_are_inverse()
    {
        var img = new PixelBuffer(3, 2, [1, 2, 3, 4, 5, 6]);
        var ccw = img.RotateCounterClockwise();
        Assert.Equal(2, ccw.Width);
        Assert.Equal(3, ccw.Height);
        Assert.Equal(new uint[] { 3, 6, 2, 5, 1, 4 }, ccw.Pixels);
        Assert.Equal(img.Pixels, ccw.RotateClockwise().Pixels);
    }

    [Fact]
    public void Downscale_averages_blocks()
    {
        var img = new PixelBuffer(4, 2, [0xFF000000, 0xFF000000, 0xFFFFFFFF, 0xFFFFFFFF, 0xFF000000, 0xFF000000, 0xFFFFFFFF, 0xFFFFFFFF]);
        var s = img.Downscale(2);
        Assert.Equal(2, s.Width);
        Assert.Equal(1, s.Height);
        Assert.Equal(0xFF000000u, s.Pixels[0]);
        Assert.Equal(0xFFFFFFFFu, s.Pixels[1]);
    }

    [Fact]
    public void Bgra_bytes_are_converted_to_argb()
    {
        var b = PixelBuffer.FromBgra(1, 1, [0x5D, 0x5E, 0xD5, 0xFF]);
        Assert.Equal(0xFFD55E5Du, b.Pixels[0]);
    }

    [Fact]
    public void Smart_crop_moves_towards_interesting_area()
    {
        // Справа насыщенное пятно, слева серо: кадр 1:1 из 3:1 должен уехать вправо.
        var img = Stripes(300, 100, 0xFF777777, 0xFF777777, 0xFFE91E63);
        var (x, _, w, h) = WallpaperAnalyzer.SmartCrop(img, 1.0);
        Assert.Equal(100, w);
        Assert.Equal(100, h);
        Assert.True(x >= 150, $"x = {x}");
    }

    [Fact]
    public void Theme_mode_follows_preference()
    {
        var dark = WallpaperAnalyzer.Analyze(Stripes(32, 32, 0xFF101010, 0xFF3A1F2B));
        ThemePalette Gen(ThemeModePreference m) => ThemeEngine.Generate(new ThemeRequest
        {
            Wallpaper = dark,
            Options = new ThemeOptions { Mode = m, EveningWarmShift = 0 },
            Now = new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.FromHours(3)),
        });
        Assert.True(Gen(ThemeModePreference.FromWallpaper).IsDark);
        Assert.False(Gen(ThemeModePreference.Light).IsDark);
        Assert.True(Gen(ThemeModePreference.Dark).IsDark);
        Assert.False(Gen(ThemeModePreference.BySchedule).IsDark); // полдень
    }

    [Fact]
    public void Custom_seed_overrides_wallpaper()
    {
        var t = ThemeEngine.Generate(new ThemeRequest
        {
            Options = new ThemeOptions { SeedSource = SeedSource.Custom, CustomSeed = 0x4285F4, EveningWarmShift = 0, Mode = ThemeModePreference.Light },
        });
        Assert.Equal(0xFF4285F4u, t.Seed);
        Assert.Equal(new DynamicScheme(0xFF4285F4, Variant.TonalSpot, false).Get(ColorRole.Primary), t[ColorRole.Primary]);
    }

    [Fact]
    public void Evening_shifts_hue_towards_warm()
    {
        uint blue = 0xFF4285F4;
        uint shifted = ThemeEngine.ShiftTowardsWarm(blue, 10);
        double before = MathUtils.DifferenceDegrees(Hct.FromArgb(blue).Hue, ThemeEngine.WarmHue);
        double after = MathUtils.DifferenceDegrees(Hct.FromArgb(shifted).Hue, ThemeEngine.WarmHue);
        Assert.InRange(before - after, 9, 11);
        Assert.Equal(0xFF808080u, ThemeEngine.ShiftTowardsWarm(0xFF808080, 10));
    }

    [Fact]
    public void Now_playing_blends_seed()
    {
        var req = new ThemeRequest
        {
            Options = new ThemeOptions { SeedSource = SeedSource.NowPlaying, NowPlayingBlend = 1 },
            NowPlayingSeed = 0xFF00897B,
        };
        var seed = ThemeEngine.ResolveSeed(req);
        Assert.True(Cam16.FromArgb(seed).Distance(Cam16.FromArgb(0xFF00897B)) < 2);
    }

    [Fact]
    public void Accent_palette_goes_from_light_to_dark_and_roundtrips_registry_blob()
    {
        var scheme = new DynamicScheme(0xFFD55E5D, Variant.TonalSpot, false);
        var p = WindowsAccentPalette.FromScheme(scheme);
        var tones = p.All.Select(ColorUtils.LstarFromArgb).ToList();
        for (int i = 1; i < tones.Count; i++) Assert.True(tones[i] < tones[i - 1]);
        var blob = p.ToRegistryBlob();
        Assert.Equal(32, blob.Length);
        Assert.Equal(p, WindowsAccentPalette.FromRegistryBlob(blob));
    }

    [Fact]
    public void Abgr_conversion_swaps_red_and_blue()
    {
        Assert.Equal(0xFF5D5ED5u, WindowsAccentPalette.ToAbgr(0xFFD55E5D));
        Assert.Equal(0xFFD55E5Du, WindowsAccentPalette.FromAbgr(0xFF5D5ED5));
    }

    [Fact]
    public void Glass_tokens_follow_glow_mode()
    {
        var scheme = new DynamicScheme(0xFFD55E5D, Variant.TonalSpot, true);
        Assert.Empty(GlassTokens.From(scheme, new GlassStyle { GlowMode = GlowMode.Off }).GlowStops);
        Assert.Equal(4, GlassTokens.From(scheme, new GlassStyle { GlowMode = GlowMode.Aurora }).GlowStops.Count);
        var frosted = GlassTokens.From(scheme, new GlassStyle { Frost = 1 });
        var clear = GlassTokens.From(scheme, new GlassStyle { Frost = 0 });
        Assert.True(frosted.BlurRadius > clear.BlurRadius);
    }

    [Fact]
    public void Terminal_palette_has_readable_colors()
    {
        var theme = ThemeEngine.Generate(new ThemeRequest { Options = new ThemeOptions { Mode = ThemeModePreference.Dark, SeedSource = SeedSource.Custom, CustomSeed = 0xFFD55E5D } });
        var a = AnsiPalette.From(theme);
        foreach (var c in new[] { a.Red, a.Green, a.Yellow, a.Blue, a.Purple, a.Cyan, a.Foreground })
            Assert.True(Contrast.RatioOfArgb(c, a.Background) >= 4.5, ColorUtils.ToHex(c));
    }

    [Fact]
    public void Exporters_produce_expected_structure()
    {
        var theme = ThemeEngine.Generate(new ThemeRequest { Options = new ThemeOptions { SeedSource = SeedSource.Custom, CustomSeed = 0xFFD55E5D } });
        var scheme = ThemeExporters.WindowsTerminalScheme(theme);
        Assert.Equal("Imagine", scheme["name"]!.GetValue<string>());
        Assert.Equal(21, scheme.Count);
        var css = ThemeExporters.Css(theme);
        Assert.Contains("--md-sys-color-primary-container:", css);
        Assert.Contains("@media (prefers-color-scheme:", css);
        var json = ThemeExporters.Json(theme);
        Assert.Contains("\"windowsAccent\"", json);
        var themeFile = ThemeExporters.WindowsThemeFile(theme, @"C:\wall.jpg");
        Assert.Contains("ColorizationColor=0XC4", themeFile);
        Assert.Contains(@"Wallpaper=C:\wall.jpg", themeFile);
        Assert.True(ThemeExporters.VsCodeColorCustomizations(theme).Count > 30);
    }

    [Fact]
    public void Preview_lists_all_variants()
    {
        Assert.Equal(Enum.GetValues<Variant>().Length, ThemeEngine.PreviewVariants(0xFFD55E5D, false).Count);
    }
}
