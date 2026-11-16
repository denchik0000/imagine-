namespace Imagine.Core.Shades;

/// <summary>
/// Отслеживает, как пользователь тянет шторку (мышью, пальцем или тачпадом), и решает,
/// открыть её до конца или вернуть, по положению и скорости — как в Android.
/// </summary>
public sealed class ShadeDragTracker
{
    private readonly List<(long Ts, double Y)> _samples = [];
    private double _startY;

    public ShadeDragTracker(double shadeHeight) => ShadeHeight = shadeHeight;

    public double ShadeHeight { get; set; }

    /// <summary>Порог скорости «броска», px/с.</summary>
    public double FlingVelocity { get; set; } = 900;

    /// <summary>Доля высоты, после которой шторка открывается и без броска.</summary>
    public double OpenThreshold { get; set; } = 0.4;

    public bool IsDragging { get; private set; }

    /// <summary>0 — закрыта, 1 — открыта. Больше 1 — «резиновое» перетягивание.</summary>
    public double Progress { get; private set; }

    public void Begin(double y, long ts, double initialProgress = 0)
    {
        IsDragging = true;
        _samples.Clear();
        _startY = y - initialProgress * ShadeHeight;
        _samples.Add((ts, y));
        Progress = initialProgress;
    }

    public double Move(double y, long ts)
    {
        if (!IsDragging) return Progress;
        _samples.Add((ts, y));
        if (_samples.Count > 8) _samples.RemoveAt(0);
        double raw = (y - _startY) / Math.Max(1, ShadeHeight);
        Progress = raw <= 1 ? Math.Max(0, raw) : 1 + Motion.Easing.RubberBand(raw - 1, 0.25);
        return Progress;
    }

    /// <summary>Скорость по последним ~80 мс, px/с (вниз положительная).</summary>
    public double Velocity
    {
        get
        {
            if (_samples.Count < 2) return 0;
            var last = _samples[^1];
            var first = _samples.LastOrDefault(s => last.Ts - s.Ts >= 80, _samples[0]);
            double dt = (last.Ts - first.Ts) / 1000.0;
            return dt <= 0 ? 0 : (last.Y - first.Y) / dt;
        }
    }

    /// <summary>Отпустили: true — открыть до конца, false — закрыть. И начальная скорость для пружины (доли высоты в секунду).</summary>
    public (bool Open, double VelocityProgressPerSecond) End()
    {
        IsDragging = false;
        double v = Velocity;
        bool open = v > FlingVelocity || (v > -FlingVelocity && Progress >= OpenThreshold);
        return (open, v / Math.Max(1, ShadeHeight));
    }
}
