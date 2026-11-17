namespace Imagine.Color;

/// <summary>
/// Ранжирует цвета изображения как кандидатов в «затравку» темы: учитывает долю оттенка
/// на снимке и насыщенность, отбрасывает почти серые цвета и слишком близкие оттенки.
/// </summary>
public static class Score
{
    private const double TargetChroma = 48.0;
    private const double WeightProportion = 0.7;
    private const double WeightChromaAbove = 0.3;
    private const double WeightChromaBelow = 0.1;
    private const double CutoffChroma = 5.0;
    private const double CutoffExcitedProportion = 0.01;

    public const uint GoogleBlue = 0xFF4285F4;

    public static IReadOnlyList<uint> Rank(
        IReadOnlyDictionary<uint, int> colorsToPopulation,
        int desired = 4,
        uint fallbackColorArgb = GoogleBlue,
        bool filter = true)
    {
        var colorsHct = new List<Hct>(colorsToPopulation.Count);
        var huePopulation = new int[360];
        double populationSum = 0;
        foreach (var (argb, population) in colorsToPopulation)
        {
            var hct = Hct.FromArgb(argb);
            colorsHct.Add(hct);
            int hue = (int)Math.Floor(hct.Hue) % 360;
            huePopulation[hue] += population;
            populationSum += population;
        }
        populationSum += 1e-5;

        var hueExcitedProportions = new double[360];
        for (int hue = 0; hue < 360; hue++)
        {
            double proportion = huePopulation[hue] / populationSum;
            for (int i = hue - 14; i < hue + 16; i++)
                hueExcitedProportions[MathUtils.SanitizeDegreesInt(i)] += proportion;
        }

        var scored = new List<(Hct Hct, double Score)>();
        foreach (var hct in colorsHct)
        {
            int hue = MathUtils.SanitizeDegreesInt(MathUtils.RoundHalfUp(hct.Hue));
            double proportion = hueExcitedProportions[hue];
            if (filter && (hct.Chroma < CutoffChroma || proportion <= CutoffExcitedProportion)) continue;
            double proportionScore = proportion * 100.0 * WeightProportion;
            double chromaWeight = hct.Chroma < TargetChroma ? WeightChromaBelow : WeightChromaAbove;
            double chromaScore = (hct.Chroma - TargetChroma) * chromaWeight;
            scored.Add((hct, proportionScore + chromaScore));
        }
        // Устойчивая сортировка по убыванию счёта.
        var sorted = scored.Select((s, i) => (s, i)).OrderByDescending(x => x.s.Score).ThenBy(x => x.i).Select(x => x.s.Hct).ToList();

        var chosen = new List<Hct>();
        for (int differenceDegrees = 90; differenceDegrees >= 15; differenceDegrees--)
        {
            chosen.Clear();
            foreach (var hct in sorted)
            {
                if (!chosen.Any(c => MathUtils.DifferenceDegrees(hct.Hue, c.Hue) < differenceDegrees)) chosen.Add(hct);
                if (chosen.Count >= desired) break;
            }
            if (chosen.Count >= desired) break;
        }

        return chosen.Count == 0 ? [fallbackColorArgb] : chosen.Select(c => c.Argb).ToList();
    }
}
