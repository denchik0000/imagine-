namespace Imagine.Color;

/// <summary>Желаемый контраст для уровней −1 (низкий), 0 (обычный), 0.5 (средний), 1 (высокий).</summary>
public sealed record ContrastCurve(double Low, double Normal, double Medium, double High)
{
    public double Get(double contrastLevel) => contrastLevel switch
    {
        <= -1.0 => Low,
        < 0.0 => MathUtils.Lerp(Low, Normal, contrastLevel + 1),
        < 0.5 => MathUtils.Lerp(Normal, Medium, contrastLevel / 0.5),
        < 1.0 => MathUtils.Lerp(Medium, High, (contrastLevel - 0.5) / 0.5),
        _ => High,
    };
}

public enum TonePolarity { Darker, Lighter, Nearer, Farther }

/// <summary>Пара ролей, между тонами которых должен сохраняться зазор <see cref="Delta"/>.</summary>
public sealed record ToneDeltaPair(DynamicColor RoleA, DynamicColor RoleB, double Delta, TonePolarity Polarity, bool StayTogether);
