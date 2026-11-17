using Imagine.Color;
using Imagine.Core.Theming;

namespace Imagine.Core.Exporters;

/// <summary>16 цветов терминала, гармонизированные с палитрой обоев.</summary>
public sealed record AnsiPalette(
    uint Black, uint Red, uint Green, uint Yellow, uint Blue, uint Purple, uint Cyan, uint White,
    uint BrightBlack, uint BrightRed, uint BrightGreen, uint BrightYellow, uint BrightBlue, uint BrightPurple, uint BrightCyan, uint BrightWhite,
    uint Background, uint Foreground, uint Cursor, uint Selection)
{
    private static readonly uint[] Base = [0xFFE53935, 0xFF43A047, 0xFFFDD835, 0xFF1E88E5, 0xFF8E24AA, 0xFF00ACC1];

    public static AnsiPalette From(ThemePalette theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        bool dark = theme.IsDark;
        uint seed = theme.Seed;
        // Классические цвета слегка поворачиваем к затравке, тон подбираем под контраст с фоном.
        uint Tone(uint baseColor, double tone)
        {
            var h = Hct.FromArgb(Blend.Harmonize(baseColor, seed));
            return Hct.From(h.Hue, Math.Max(h.Chroma, 40), tone).Argb;
        }
        double normal = dark ? 70 : 42, bright = dark ? 82 : 30;
        uint[] n = Base.Select(c => Tone(c, normal)).ToArray();
        uint[] b = Base.Select(c => Tone(c, bright)).ToArray();
        var neutral = theme.Scheme.NeutralPalette;
        uint bg = theme[ColorRole.Surface];
        uint fg = theme[ColorRole.OnSurface];
        return new AnsiPalette(
            neutral.Tone(dark ? 20 : 15), n[0], n[1], n[2], n[3], n[4], n[5], neutral.Tone(dark ? 85 : 70),
            neutral.Tone(dark ? 45 : 40), b[0], b[1], b[2], b[3], b[4], b[5], neutral.Tone(dark ? 98 : 90),
            bg, fg, theme[ColorRole.Primary], theme[ColorRole.PrimaryContainer]);
    }
}
