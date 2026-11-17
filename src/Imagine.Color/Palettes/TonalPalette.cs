using System.Collections.Concurrent;

namespace Imagine.Color;

/// <summary>Тональная палитра: один оттенок и хрома, тон 0..100 задаётся при запросе.</summary>
public sealed class TonalPalette
{
    private readonly ConcurrentDictionary<double, uint> _cache = new();

    public double Hue { get; }
    public double Chroma { get; }
    public Hct KeyColor { get; }

    private TonalPalette(double hue, double chroma, Hct keyColor)
    {
        Hue = hue;
        Chroma = chroma;
        KeyColor = keyColor;
    }

    public static TonalPalette FromArgb(uint argb) => FromHct(Hct.FromArgb(argb));

    public static TonalPalette FromHct(Hct hct) => new(hct.Hue, hct.Chroma, hct);

    public static TonalPalette FromHueAndChroma(double hue, double chroma)
        => new(hue, chroma, new KeyColorFinder(hue, chroma).Create());

    /// <summary>Цвет палитры с заданным тоном (0 — чёрный, 100 — белый).</summary>
    public uint Tone(double tone)
        => _cache.GetOrAdd(tone, t => t == 99 && Hct.IsYellow(Hue)
            ? AverageArgb(Tone(98), Tone(100))
            : Hct.From(Hue, Chroma, t).Argb);

    public Hct GetHct(double tone) => Hct.FromArgb(Tone(tone));

    private static uint AverageArgb(uint a, uint b)
    {
        int r = MathUtils.RoundHalfUp((ColorUtils.RedFromArgb(a) + ColorUtils.RedFromArgb(b)) / 2.0);
        int g = MathUtils.RoundHalfUp((ColorUtils.GreenFromArgb(a) + ColorUtils.GreenFromArgb(b)) / 2.0);
        int bl = MathUtils.RoundHalfUp((ColorUtils.BlueFromArgb(a) + ColorUtils.BlueFromArgb(b)) / 2.0);
        return ColorUtils.ArgbFromRgb(r, g, bl);
    }

    /// <summary>Ищет ключевой цвет палитры: ближайший к тону 50, у которого хрома не ниже запрошенной.</summary>
    private sealed class KeyColorFinder(double hue, double requestedChroma)
    {
        private const double MaxChromaValue = 200.0;
        private readonly Dictionary<int, double> _chromaCache = [];

        public Hct Create()
        {
            const int pivotTone = 50;
            const int toneStepSize = 1;
            const double epsilon = 0.01;
            int lowerTone = 0, upperTone = 100;
            while (lowerTone < upperTone)
            {
                int midTone = (lowerTone + upperTone) / 2;
                bool isAscending = MaxChroma(midTone) < MaxChroma(midTone + toneStepSize);
                bool sufficientChroma = MaxChroma(midTone) >= requestedChroma - epsilon;
                if (sufficientChroma)
                {
                    if (Math.Abs(lowerTone - pivotTone) < Math.Abs(upperTone - pivotTone))
                    {
                        upperTone = midTone;
                    }
                    else
                    {
                        if (lowerTone == midTone) return Hct.From(hue, requestedChroma, lowerTone);
                        lowerTone = midTone;
                    }
                }
                else if (isAscending)
                {
                    lowerTone = midTone + toneStepSize;
                }
                else
                {
                    upperTone = midTone;
                }
            }
            return Hct.From(hue, requestedChroma, lowerTone);
        }

        private double MaxChroma(int tone)
        {
            if (_chromaCache.TryGetValue(tone, out var c)) return c;
            c = Hct.From(hue, MaxChromaValue, tone).Chroma;
            _chromaCache[tone] = c;
            return c;
        }
    }
}
