namespace Imagine.Color;

/// <summary>
/// HCT — оттенок (Hue, 0..360), хрома (Chroma, 0..~150) и тон (Tone, L* 0..100).
/// Тон соответствует воспринимаемой светлоте, поэтому контраст считается по разнице тонов.
/// Неизменяемый тип: методы With* возвращают новый цвет.
/// </summary>
public sealed class Hct : IEquatable<Hct>
{
    public double Hue { get; }
    public double Chroma { get; }
    public double Tone { get; }
    public uint Argb { get; }

    private Hct(uint argb)
    {
        var cam = Cam16.FromArgb(argb);
        Hue = cam.Hue;
        Chroma = cam.Chroma;
        Tone = ColorUtils.LstarFromArgb(argb);
        Argb = argb;
    }

    public static Hct From(double hue, double chroma, double tone) => new(HctSolver.SolveToArgb(hue, chroma, tone));

    public static Hct FromArgb(uint argb) => new(argb);

    public Hct WithHue(double hue) => From(hue, Chroma, Tone);
    public Hct WithChroma(double chroma) => From(Hue, chroma, Tone);
    public Hct WithTone(double tone) => From(Hue, Chroma, tone);

    public static bool IsBlue(double hue) => hue >= 250 && hue < 270;
    public static bool IsYellow(double hue) => hue >= 105 && hue < 125;
    public static bool IsCyan(double hue) => hue >= 170 && hue < 207;

    /// <summary>Пересчитывает цвет так, как он выглядел бы в других условиях просмотра.</summary>
    public Hct InViewingConditions(ViewingConditions vc)
    {
        var cam = Cam16.FromArgb(Argb);
        var viewed = cam.XyzInViewingConditions(vc);
        var recast = Cam16.FromXyzInViewingConditions(viewed[0], viewed[1], viewed[2], ViewingConditions.Make());
        return From(recast.Hue, recast.Chroma, ColorUtils.LstarFromY(viewed[1]));
    }

    public bool Equals(Hct? other) => other is not null && other.Argb == Argb;
    public override bool Equals(object? obj) => Equals(obj as Hct);
    public override int GetHashCode() => (int)Argb;
    public override string ToString() => $"HCT({Hue:0}, {Chroma:0}, {Tone:0}) {ColorUtils.ToHex(Argb)}";
}
