namespace Imagine.Color.Tests;

public class PaletteTests
{
    [Fact]
    public void Tonal_palettes_match_reference()
    {
        foreach (var e in Golden.Root.GetProperty("palettes").EnumerateArray())
        {
            var p = TonalPalette.FromHueAndChroma(e.GetProperty("hue").GetDouble(), e.GetProperty("chroma").GetDouble());
            Assert.Equal(Golden.Hex(e.GetProperty("key").GetUInt32()), Golden.Hex(p.KeyColor.Argb));
            foreach (var t in e.GetProperty("tones").EnumerateObject())
                Assert.Equal(Golden.Hex(t.Value.GetUInt32()), Golden.Hex(p.Tone(double.Parse(t.Name, System.Globalization.CultureInfo.InvariantCulture))));
        }
    }

    [Fact]
    public void Temperature_cache_matches_reference()
    {
        foreach (var e in Golden.Root.GetProperty("temperature").EnumerateArray())
        {
            var tc = new TemperatureCache(Hct.FromArgb(e.GetProperty("argb").GetUInt32()));
            Assert.Equal(Golden.Hex(e.GetProperty("complement").GetUInt32()), Golden.Hex(tc.Complement.Argb));
            var expected = e.GetProperty("analogous").EnumerateArray().Select(x => Golden.Hex(x.GetUInt32())).ToArray();
            Assert.Equal(expected, tc.Analogous(5, 12).Select(h => Golden.Hex(h.Argb)).ToArray());
            var expected3 = e.GetProperty("analogous3_6").EnumerateArray().Select(x => Golden.Hex(x.GetUInt32())).ToArray();
            Assert.Equal(expected3, tc.Analogous(3, 6).Select(h => Golden.Hex(h.Argb)).ToArray());
        }
    }

    [Fact]
    public void Harmonize_rotates_at_most_15_degrees_towards_source()
    {
        uint red = 0xFFFF0000, blue = 0xFF4285F4;
        var before = Hct.FromArgb(red);
        var after = Hct.FromArgb(Blend.Harmonize(red, blue));
        Assert.InRange(MathUtils.DifferenceDegrees(before.Hue, after.Hue), 1, 15.5);
    }
}
