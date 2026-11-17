namespace Imagine.Color.Tests;

public class QuantizeTests
{
    // Эталон на C++ стартует k-средние от rand() из libc, а мы, как Java-версия Google, от java.util.Random(0x42688).
    // Поэтому точного совпадения кластеров нет, но главные цвета обоев должны совпадать перцептивно.
    [Fact]
    public void Wallpaper_seed_colors_match_reference_perceptually()
    {
        var pixels = Golden.LoadWallpaper();
        var wp = Golden.Root.GetProperty("wallpaper");
        Assert.Equal(wp.GetProperty("width").GetInt32() * wp.GetProperty("height").GetInt32(), pixels.Length);

        var ranked = Score.Rank(QuantizerCelebi.Quantize(pixels, 128));
        var expected = wp.GetProperty("ranked").EnumerateArray().Select(x => x.GetUInt32()).ToArray();

        for (int i = 0; i < 2; i++)
        {
            double distance = Cam16.FromArgb(expected[i]).Distance(Cam16.FromArgb(ranked[i]));
            Assert.True(distance < 5, $"цвет №{i + 1}: эталон {Golden.Hex(expected[i])}, получено {Golden.Hex(ranked[i])}, ΔE={distance:0.0}");
        }
    }

    [Fact]
    public void Wallpaper_quantization_is_deterministic_and_covers_all_pixels()
    {
        var pixels = Golden.LoadWallpaper();
        var a = QuantizerCelebi.Quantize(pixels, 128);
        var b = QuantizerCelebi.Quantize(pixels, 128);
        Assert.Equal(a.OrderBy(x => x.Key), b.OrderBy(x => x.Key));
        Assert.Equal(pixels.Length, a.Values.Sum());
        Assert.InRange(a.Count, 16, 128);
    }

    [Fact]
    public void Single_color_image_returns_that_color()
    {
        var pixels = Enumerable.Repeat(0xFFD55E5Du, 1000).ToArray();
        var q = QuantizerCelebi.Quantize(pixels, 128);
        Assert.Single(q);
        Assert.Equal(0xFFD55E5Du, Score.Rank(q)[0]);
    }

    [Fact]
    public void Gray_image_falls_back_to_google_blue()
    {
        var pixels = Enumerable.Repeat(0xFF808080u, 100).ToArray();
        Assert.Equal(Score.GoogleBlue, Score.Rank(QuantizerCelebi.Quantize(pixels))[0]);
    }

    [Fact]
    public void Transparent_pixels_are_ignored()
    {
        var pixels = Enumerable.Repeat(0x00FF0000u, 500).Concat(Enumerable.Repeat(0xFF0000FFu, 10)).ToArray();
        var q = QuantizerCelebi.Quantize(pixels);
        Assert.Equal(10, q.Values.Sum());
    }
}
