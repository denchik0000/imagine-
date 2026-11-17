namespace Imagine.Color.Tests;

public class HctTests
{
    [Fact]
    public void Hct_from_argb_matches_reference()
    {
        foreach (var e in Golden.Root.GetProperty("hct").EnumerateArray())
        {
            var hct = Hct.FromArgb(e.GetProperty("argb").GetUInt32());
            Assert.Equal(e.GetProperty("hue").GetDouble(), hct.Hue, 6);
            Assert.Equal(e.GetProperty("chroma").GetDouble(), hct.Chroma, 6);
            Assert.Equal(e.GetProperty("tone").GetDouble(), hct.Tone, 6);
        }
    }

    [Fact]
    public void Solver_matches_reference_on_grid()
    {
        int checkedCount = 0;
        foreach (var e in Golden.Root.GetProperty("solve").EnumerateArray())
        {
            double h = e[0].GetDouble(), c = e[1].GetDouble(), t = e[2].GetDouble();
            uint expected = e[3].GetUInt32();
            var actual = Hct.From(h, c, t).Argb;
            Assert.True(expected == actual, $"HCT({h},{c},{t}): ожидалось {Golden.Hex(expected)}, получено {Golden.Hex(actual)}");
            checkedCount++;
        }
        Assert.True(checkedCount > 500);
    }

    [Theory]
    [InlineData(0xFFFF0000u)]
    [InlineData(0xFF00FF00u)]
    [InlineData(0xFF0000FFu)]
    [InlineData(0xFFD55E5Du)]
    [InlineData(0xFF808080u)]
    public void Roundtrip_through_hct_is_lossless_for_srgb_colors(uint argb)
    {
        var hct = Hct.FromArgb(argb);
        Assert.Equal(argb, Hct.From(hct.Hue, hct.Chroma, hct.Tone).Argb);
    }

    [Fact]
    public void Tone_is_preserved_when_chroma_is_out_of_gamut()
    {
        var hct = Hct.From(120, 200, 70);
        Assert.InRange(hct.Tone, 69.5, 70.5);
        Assert.True(hct.Chroma < 200);
    }

    [Fact]
    public void Hex_parsing_supports_short_and_alpha_forms()
    {
        Assert.Equal(0xFFFF0000u, ColorUtils.FromHex("#f00"));
        Assert.Equal(0xFFD55E5Du, ColorUtils.FromHex("D55E5D"));
        Assert.Equal(0x80D55E5Du, ColorUtils.FromHex("#80D55E5D"));
        Assert.False(ColorUtils.TryFromHex("#zzz", out _));
        Assert.Equal("#D55E5D", ColorUtils.ToHex(0xFFD55E5D));
    }

    [Fact]
    public void Contrast_ratio_of_black_and_white_is_21()
    {
        Assert.Equal(21.0, Contrast.RatioOfArgb(0xFF000000, 0xFFFFFFFF), 3);
        Assert.Equal(1.0, Contrast.RatioOfTones(50, 50), 6);
    }
}
