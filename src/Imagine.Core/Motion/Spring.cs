namespace Imagine.Core.Motion;

/// <summary>
/// Параметры пружины в терминах дизайнеров: жёсткость (stiffness) и коэффициент демпфирования (dampingRatio),
/// как SpringForce в Android. dampingRatio 1 — без перелёта, меньше 1 — «пружинит».
/// </summary>
public readonly record struct SpringSpec(double Stiffness, double DampingRatio)
{
    /// <summary>Мягкая «жидкая» пружина для шторок (Fluid Design ColorOS).</summary>
    public static readonly SpringSpec Fluid = new(380, 0.82);
    /// <summary>Быстрая и упругая для плиток и кнопок.</summary>
    public static readonly SpringSpec Bouncy = new(600, 0.6);
    /// <summary>Без перелёта для закрытия.</summary>
    public static readonly SpringSpec Snappy = new(900, 1.0);
    /// <summary>Медленная плавная для переезда часов.</summary>
    public static readonly SpringSpec Gentle = new(120, 0.9);

    public double NaturalFrequency => Math.Sqrt(Stiffness);
}

/// <summary>
/// Пружина с аналитическим решением (без накопления ошибок шага): позиция и скорость
/// в любой момент времени для недо-, критически и пере-демпфированного случая.
/// Позволяет перехватывать анимацию на лету: новая цель стартует с текущей скорости.
/// </summary>
public sealed class Spring
{
    private double _startValue;
    private double _startVelocity;
    private double _elapsed;

    public SpringSpec Spec { get; set; }
    public double Target { get; private set; }
    public double Value { get; private set; }
    public double Velocity { get; private set; }

    /// <summary>Порог покоя по позиции.</summary>
    public double RestDisplacement { get; set; } = 0.001;
    /// <summary>Порог покоя по скорости (единиц в секунду).</summary>
    public double RestVelocity { get; set; } = 0.01;

    public Spring(double initial, SpringSpec spec)
    {
        Value = initial;
        Target = initial;
        _startValue = initial;
        Spec = spec;
    }

    public bool IsAtRest => Math.Abs(Value - Target) < RestDisplacement && Math.Abs(Velocity) < RestVelocity;

    /// <summary>Новая цель; движение продолжается с текущими позицией и скоростью.</summary>
    public void AnimateTo(double target, double? initialVelocity = null)
    {
        Target = target;
        _startValue = Value;
        _startVelocity = initialVelocity ?? Velocity;
        Velocity = _startVelocity;
        _elapsed = 0;
    }

    /// <summary>Мгновенно поставить в позицию (например, пока пользователь тянет шторку пальцем).</summary>
    public void SnapTo(double value)
    {
        Value = value;
        Target = value;
        Velocity = 0;
        _startValue = value;
        _startVelocity = 0;
        _elapsed = 0;
    }

    /// <summary>Продвигает время на <paramref name="dtSeconds"/>. Возвращает true, пока пружина в движении.</summary>
    public bool Step(double dtSeconds)
    {
        _elapsed += Math.Max(0, dtSeconds);
        (Value, Velocity) = Evaluate(_startValue - Target, _startVelocity, _elapsed, Spec);
        Value += Target;
        if (IsAtRest)
        {
            Value = Target;
            Velocity = 0;
            return false;
        }
        return true;
    }

    /// <summary>Аналитическое решение x'' + 2ζω x' + ω² x = 0.</summary>
    public static (double Displacement, double Velocity) Evaluate(double x0, double v0, double t, SpringSpec spec)
    {
        double w = spec.NaturalFrequency;
        double z = spec.DampingRatio;
        if (z < 1)
        {
            double wd = w * Math.Sqrt(1 - z * z);
            double a = x0;
            double b = (v0 + z * w * x0) / wd;
            double e = Math.Exp(-z * w * t);
            double cos = Math.Cos(wd * t), sin = Math.Sin(wd * t);
            double x = e * (a * cos + b * sin);
            double v = e * ((b * wd - z * w * a) * cos - (a * wd + z * w * b) * sin);
            return (x, v);
        }
        if (Math.Abs(z - 1) < 1e-9)
        {
            double a = x0;
            double b = v0 + w * x0;
            double e = Math.Exp(-w * t);
            return (e * (a + b * t), e * (b - w * (a + b * t)));
        }
        {
            double s = Math.Sqrt(z * z - 1);
            double r1 = -w * (z - s), r2 = -w * (z + s);
            double c2 = (v0 - r1 * x0) / (r2 - r1);
            double c1 = x0 - c2;
            double e1 = Math.Exp(r1 * t), e2 = Math.Exp(r2 * t);
            return (c1 * e1 + c2 * e2, c1 * r1 * e1 + c2 * r2 * e2);
        }
    }

    /// <summary>Примерное время до покоя (для Composition-анимаций с фиксированной длительностью).</summary>
    public static TimeSpan EstimateSettleTime(SpringSpec spec, double distance = 1, double threshold = 0.001)
    {
        double t = 0;
        const double dt = 1.0 / 240;
        double x0 = distance;
        while (t < 5)
        {
            t += dt;
            var (x, v) = Evaluate(x0, 0, t, spec);
            if (Math.Abs(x) < threshold * Math.Abs(distance) && Math.Abs(v) < threshold * 10) break;
        }
        return TimeSpan.FromSeconds(t);
    }
}
