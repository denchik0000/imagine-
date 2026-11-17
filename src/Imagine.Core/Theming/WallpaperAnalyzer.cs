using Imagine.Color;

namespace Imagine.Core.Theming;

/// <summary>
/// Анализ обоев: квантизация Celebi + оценка Score (как в Android), средние тоны и подсказки по ориентации.
/// </summary>
public static class WallpaperAnalyzer
{
    /// <summary>До какого размера уменьшать снимок перед анализом. 128 px дают тот же результат, что и полный размер, в сотни раз быстрее.</summary>
    public const int AnalysisSide = 128;

    public static WallpaperAnalysis Analyze(PixelBuffer image, int maxSeedCandidates = 4)
    {
        ArgumentNullException.ThrowIfNull(image);
        var small = image.Downscale(AnalysisSide);
        var quantized = QuantizerCelebi.Quantize(small.Pixels, 128);
        var seeds = Score.Rank(quantized, desired: maxSeedCandidates);

        long total = quantized.Values.Sum();
        var dominant = quantized
            .OrderByDescending(kv => kv.Value)
            .Take(12)
            .Select(kv => new DominantColor(kv.Key, total == 0 ? 0 : (double)kv.Value / total))
            .ToList();

        double toneSum = 0, topToneSum = 0;
        int colorful = 0, counted = 0, topCounted = 0;
        int topRows = Math.Max(1, small.Height / 3);
        // Тоны считаем по каждому пикселю уменьшенной копии: быстро и достаточно точно.
        var toneCache = new Dictionary<uint, (double Tone, double Chroma)>();
        for (int y = 0; y < small.Height; y++)
        {
            for (int x = 0; x < small.Width; x++)
            {
                uint p = small[x, y] | 0xFF000000;
                if (!toneCache.TryGetValue(p, out var tc))
                {
                    var cam = Cam16.FromArgb(p);
                    tc = (ColorUtils.LstarFromArgb(p), cam.Chroma);
                    toneCache[p] = tc;
                }
                toneSum += tc.Tone;
                counted++;
                if (tc.Chroma >= 15) colorful++;
                if (y < topRows)
                {
                    topToneSum += tc.Tone;
                    topCounted++;
                }
            }
        }

        return new WallpaperAnalysis
        {
            SeedCandidates = seeds,
            DominantColors = dominant,
            AverageTone = counted == 0 ? 50 : toneSum / counted,
            TopAreaTone = topCounted == 0 ? 50 : topToneSum / topCounted,
            Colorfulness = counted == 0 ? 0 : (double)colorful / counted,
            Width = image.Width,
            Height = image.Height,
        };
    }

    /// <summary>
    /// Нужен ли поворот, чтобы снимок лёг на экран без огромных полей:
    /// портретный кадр на альбомном мониторе (и наоборот), у которого после поворота соотношение сторон почти как у экрана.
    /// </summary>
    public static bool SuggestsRotation(int imageWidth, int imageHeight, int screenWidth, int screenHeight, double tolerance = 0.08)
    {
        if (imageWidth <= 0 || imageHeight <= 0 || screenWidth <= 0 || screenHeight <= 0) return false;
        bool imagePortrait = imageHeight > imageWidth;
        bool screenPortrait = screenHeight > screenWidth;
        if (imagePortrait == screenPortrait) return false;
        double rotatedAspect = (double)imageHeight / imageWidth;
        double screenAspect = (double)screenWidth / screenHeight;
        return Math.Abs(rotatedAspect - screenAspect) / screenAspect <= tolerance;
    }

    /// <summary>
    /// «Умный кроп»: окно с соотношением сторон экрана, сдвинутое к самой «интересной» области (где больше контраста и насыщенности).
    /// </summary>
    public static (int X, int Y, int Width, int Height) SmartCrop(PixelBuffer image, double targetAspect)
    {
        ArgumentNullException.ThrowIfNull(image);
        int cw = image.Width, ch = (int)Math.Round(image.Width / targetAspect);
        if (ch > image.Height)
        {
            ch = image.Height;
            cw = (int)Math.Round(image.Height * targetAspect);
        }
        var small = image.Downscale(96);
        double sx = (double)small.Width / image.Width, sy = (double)small.Height / image.Height;
        int scw = Math.Max(1, (int)(cw * sx)), sch = Math.Max(1, (int)(ch * sy));

        // Карта «интересности»: хрома + локальный перепад тона.
        var interest = new double[small.Width * small.Height];
        for (int y = 0; y < small.Height; y++)
        {
            for (int x = 0; x < small.Width; x++)
            {
                uint p = small[x, y] | 0xFF000000;
                double tone = ColorUtils.LstarFromArgb(p);
                double chroma = Cam16.FromArgb(p).Chroma;
                double grad = 0;
                if (x + 1 < small.Width) grad += Math.Abs(tone - ColorUtils.LstarFromArgb(small[x + 1, y] | 0xFF000000));
                if (y + 1 < small.Height) grad += Math.Abs(tone - ColorUtils.LstarFromArgb(small[x, y + 1] | 0xFF000000));
                interest[y * small.Width + x] = chroma * 0.6 + grad;
            }
        }

        // Интегральное изображение для быстрых сумм по окну.
        var integral = new double[(small.Width + 1) * (small.Height + 1)];
        int iw = small.Width + 1;
        for (int y = 1; y <= small.Height; y++)
            for (int x = 1; x <= small.Width; x++)
                integral[y * iw + x] = interest[(y - 1) * small.Width + (x - 1)] + integral[(y - 1) * iw + x] + integral[y * iw + x - 1] - integral[(y - 1) * iw + x - 1];

        double best = double.MinValue;
        int bx = 0, by = 0;
        for (int y = 0; y + sch <= small.Height; y++)
        {
            for (int x = 0; x + scw <= small.Width; x++)
            {
                double sum = integral[(y + sch) * iw + x + scw] - integral[y * iw + x + scw] - integral[(y + sch) * iw + x] + integral[y * iw + x];
                // Лёгкое предпочтение центру, чтобы при равных условиях не уезжать к краю.
                double cx = (x + scw / 2.0) / small.Width - 0.5, cy = (y + sch / 2.0) / small.Height - 0.5;
                sum *= 1.0 - 0.15 * (cx * cx + cy * cy);
                if (sum > best) { best = sum; bx = x; by = y; }
            }
        }
        int ox = (int)Math.Round(bx / sx), oy = (int)Math.Round(by / sy);
        ox = Math.Clamp(ox, 0, image.Width - cw);
        oy = Math.Clamp(oy, 0, image.Height - ch);
        return (ox, oy, cw, ch);
    }
}
