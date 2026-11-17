namespace Imagine.Color;

/// <summary>
/// Квантизатор Celebi: Wu для стартовых кластеров, затем взвешенные k-средние.
/// Это тот же конвейер, которым Android выбирает цвета из обоев.
/// </summary>
public static class QuantizerCelebi
{
    /// <param name="pixels">Пиксели ARGB. Непрозрачность не требуется: прозрачные пиксели отбрасываются.</param>
    /// <param name="maxColors">Максимум цветов (Android использует 128).</param>
    /// <returns>Цвет → число пикселей.</returns>
    public static Dictionary<uint, int> Quantize(ReadOnlySpan<uint> pixels, int maxColors = 128)
    {
        var opaque = new List<uint>(pixels.Length);
        foreach (var p in pixels)
            if (ColorUtils.IsOpaque(p)) opaque.Add(p);
        var span = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(opaque);
        var wu = new QuantizerWu().Quantize(span, maxColors);
        return QuantizerWsmeans.Quantize(span, wu, maxColors);
    }
}
