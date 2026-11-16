using Imagine.Core.Motion;

namespace Imagine.Core.Tests;

public class MotionTests
{
    private static (double Max, double Final) Run(SpringSpec spec)
    {
        var s = new Spring(0, spec);
        s.AnimateTo(1);
        double max = 0;
        for (int i = 0; i < 600 && s.Step(1 / 120.0); i++) max = Math.Max(max, s.Value);
        return (Math.Max(max, s.Value), s.Value);
    }

    [Fact]
    public void Critically_damped_spring_does_not_overshoot()
    {
        var (max, final) = Run(SpringSpec.Snappy);
        Assert.True(max <= 1.0001);
        Assert.Equal(1, final, 3);
    }

    [Fact]
    public void Underdamped_spring_overshoots_and_settles()
    {
        var (max, final) = Run(SpringSpec.Bouncy);
        Assert.True(max > 1.01);
        Assert.Equal(1, final, 3);
    }

    [Fact]
    public void Overdamped_spring_settles()
    {
        var (_, final) = Run(new SpringSpec(300, 1.6));
        Assert.Equal(1, final, 3);
    }

    [Fact]
    public void Retargeting_keeps_velocity()
    {
        var s = new Spring(0, SpringSpec.Fluid);
        s.AnimateTo(1);
        for (int i = 0; i < 6; i++) s.Step(1 / 120.0);
        double v = s.Velocity;
        Assert.True(v > 0);
        s.AnimateTo(-1);
        Assert.Equal(v, s.Velocity);
    }

    [Fact]
    public void Analytic_solution_matches_numeric_integration()
    {
        var spec = SpringSpec.Fluid;
        double x = 1, v = 0, dt = 1e-5;
        for (double t = 0; t < 0.3; t += dt)
        {
            double a = -spec.Stiffness * x - 2 * spec.DampingRatio * spec.NaturalFrequency * v;
            v += a * dt;
            x += v * dt;
        }
        var (ax, _) = Spring.Evaluate(1, 0, 0.3, spec);
        Assert.Equal(ax, x, 2);
    }

    [Fact]
    public void Bezier_endpoints_and_monotonicity()
    {
        Assert.Equal(0, Easing.Emphasized(0), 6);
        Assert.Equal(1, Easing.Emphasized(1), 6);
        double prev = 0;
        for (double t = 0.05; t <= 1; t += 0.05)
        {
            double y = Easing.CubicBezier(0.2, 0, 0, 1, t);
            Assert.True(y >= prev);
            prev = y;
        }
    }

    [Fact]
    public void Rubber_band_never_exceeds_dimension()
    {
        Assert.True(Easing.RubberBand(10_000, 100) < 100);
        Assert.Equal(0, Easing.RubberBand(0, 100));
    }

    [Fact]
    public void Settle_time_is_reasonable()
    {
        var t = Spring.EstimateSettleTime(SpringSpec.Fluid);
        Assert.InRange(t.TotalMilliseconds, 150, 1500);
    }
}
