using Imagine.Core.Theming;

namespace Imagine.Core.Services;

public interface IImageDecoder
{
    /// <summary>Читает картинку и уменьшает её так, чтобы длинная сторона была не больше <paramref name="maxSide"/>. Учитывает EXIF-поворот.</summary>
    Task<PixelBuffer> DecodeAsync(string path, int maxSide, CancellationToken ct = default);

    /// <summary>Размер картинки без декодирования.</summary>
    Task<(int Width, int Height)> GetSizeAsync(string path, CancellationToken ct = default);
}
