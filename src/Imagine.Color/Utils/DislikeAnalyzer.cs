namespace Imagine.Color;

/// <summary>Исправляет «неприятные» цвета (тёмные оливково-жёлтые, похожие на болотную тину).</summary>
public static class DislikeAnalyzer
{
    public static bool IsDisliked(Hct hct)
    {
        int hue = MathUtils.RoundHalfUp(hct.Hue);
        bool huePasses = hue >= 90 && hue <= 111;
        bool chromaPasses = MathUtils.RoundHalfUp(hct.Chroma) > 16;
        bool tonePasses = MathUtils.RoundHalfUp(hct.Tone) < 65;
        return huePasses && chromaPasses && tonePasses;
    }

    public static Hct FixIfDisliked(Hct hct) => IsDisliked(hct) ? Hct.From(hct.Hue, hct.Chroma, 70.0) : hct;
}
