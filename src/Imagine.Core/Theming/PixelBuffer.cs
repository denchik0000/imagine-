namespace Imagine.Core.Theming;

/// <summary>Изображение в памяти: пиксели ARGB построчно.</summary>
public sealed class PixelBuffer
{
    public int Width { get; }
    public int Height { get; }
    public uint[] Pixels { get; }

    public PixelBuffer(int width, int height, uint[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Length != width * height) throw new ArgumentException("Число пикселей не совпадает с размером.", nameof(pixels));
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public uint this[int x, int y] => Pixels[y * Width + x];

    /// <summary>Из BGRA-байтов (формат WIC/WinRT BitmapDecoder с BitmapPixelFormat.Bgra8).</summary>
    public static PixelBuffer FromBgra(int width, int height, ReadOnlySpan<byte> bgra)
    {
        if (bgra.Length < width * height * 4) throw new ArgumentException("Недостаточно байтов.", nameof(bgra));
        var px = new uint[width * height];
        for (int i = 0; i < px.Length; i++)
        {
            int o = i * 4;
            px[i] = ((uint)bgra[o + 3] << 24) | ((uint)bgra[o + 2] << 16) | ((uint)bgra[o + 1] << 8) | bgra[o];
        }
        return new PixelBuffer(width, height, px);
    }

    /// <summary>Уменьшает изображение усреднением блоков так, чтобы длинная сторона была не больше <paramref name="maxSide"/>.</summary>
    public PixelBuffer Downscale(int maxSide)
    {
        int longSide = Math.Max(Width, Height);
        if (longSide <= maxSide) return this;
        double scale = (double)maxSide / longSide;
        int w = Math.Max(1, (int)Math.Round(Width * scale));
        int h = Math.Max(1, (int)Math.Round(Height * scale));
        var result = new uint[w * h];
        for (int ty = 0; ty < h; ty++)
        {
            int y0 = ty * Height / h, y1 = Math.Max(y0 + 1, (ty + 1) * Height / h);
            for (int tx = 0; tx < w; tx++)
            {
                int x0 = tx * Width / w, x1 = Math.Max(x0 + 1, (tx + 1) * Width / w);
                long a = 0, r = 0, g = 0, b = 0, n = 0;
                for (int y = y0; y < y1; y++)
                {
                    int row = y * Width;
                    for (int x = x0; x < x1; x++)
                    {
                        uint p = Pixels[row + x];
                        a += p >> 24; r += (p >> 16) & 255; g += (p >> 8) & 255; b += p & 255; n++;
                    }
                }
                result[ty * w + tx] = ((uint)(a / n) << 24) | ((uint)(r / n) << 16) | ((uint)(g / n) << 8) | (uint)(b / n);
            }
        }
        return new PixelBuffer(w, h, result);
    }

    /// <summary>Поворот на 90° против часовой стрелки (для обоев, снятых «на боку»).</summary>
    public PixelBuffer RotateCounterClockwise()
    {
        var result = new uint[Pixels.Length];
        int nw = Height, nh = Width;
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                result[(Width - 1 - x) * nw + y] = Pixels[y * Width + x];
        return new PixelBuffer(nw, nh, result);
    }

    /// <summary>Поворот на 90° по часовой стрелке.</summary>
    public PixelBuffer RotateClockwise()
    {
        var result = new uint[Pixels.Length];
        int nw = Height, nh = Width;
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                result[x * nw + (Height - 1 - y)] = Pixels[y * Width + x];
        return new PixelBuffer(nw, nh, result);
    }

    /// <summary>Вырезает прямоугольник.</summary>
    public PixelBuffer Crop(int x, int y, int width, int height)
    {
        x = Math.Clamp(x, 0, Width - 1);
        y = Math.Clamp(y, 0, Height - 1);
        width = Math.Clamp(width, 1, Width - x);
        height = Math.Clamp(height, 1, Height - y);
        var result = new uint[width * height];
        for (int row = 0; row < height; row++)
            Array.Copy(Pixels, (y + row) * Width + x, result, row * width, width);
        return new PixelBuffer(width, height, result);
    }
}
