namespace Imagine.Core.Shades;

/// <summary>
/// Решает, когда открыть шторку по движениям мыши у верхнего края.
/// Получает сырые события (из WH_MOUSE_LL) и ничего не знает о Win32, поэтому полностью тестируется.
/// </summary>
public sealed class EdgeZoneDetector
{
    private readonly List<ScreenRect> _monitors = [];
    private EdgeSettings _settings;
    private ShadeKind? _armedZone;
    private long _armedSince;
    private int _pushCount;
    private int _wheelAccumulator;
    private long _lastWheelTs;
    private long _cooldownUntil;
    private bool _firedForCurrentStay;

    public EdgeZoneDetector(EdgeSettings settings, IEnumerable<ScreenRect> monitors)
    {
        _settings = settings;
        SetMonitors(monitors);
    }

    /// <summary>Шторка должна открыться.</summary>
    public event EventHandler<ShadeRequest>? Triggered;

    /// <summary>Курсор вошёл в зону: можно показать «намёк» (лёгкое свечение у края).</summary>
    public event EventHandler<ShadeKind?>? HintChanged;

    public bool IsFullscreenAppActive { get; set; }
    public bool IsShadeOpen { get; set; }

    public EdgeSettings Settings
    {
        get => _settings;
        set { _settings = value; Reset(); }
    }

    public void SetMonitors(IEnumerable<ScreenRect> monitors)
    {
        _monitors.Clear();
        _monitors.AddRange(monitors);
        Reset();
    }

    /// <summary>Сообщить, что шторка закрылась (включает паузу).</summary>
    public void NotifyClosed(long timestampMs)
    {
        IsShadeOpen = false;
        _cooldownUntil = timestampMs + _settings.CooldownMs;
        _firedForCurrentStay = true;
        Reset(keepFired: true);
    }

    private bool Suppressed(long ts, bool buttonDown)
        => IsShadeOpen
           || ts < _cooldownUntil
           || (_settings.DisableInFullscreen && IsFullscreenAppActive)
           || (_settings.IgnoreWhileButtonDown && buttonDown);

    /// <summary>Какая зона под точкой; null — не край или мёртвая зона.</summary>
    public ShadeKind? ZoneAt(int x, int y)
    {
        foreach (var m in _monitors)
        {
            if (x < m.Left || x >= m.Right) continue;
            if (y < m.Top || y >= m.Top + Math.Max(1, _settings.EdgeThickness)) continue;
            if (HasMonitorAbove(m, x)) continue;
            int local = x - m.Left;
            if (local < _settings.LeftDeadZone || local >= m.Width - _settings.RightDeadZone) return null;
            bool left = local < m.Width * _settings.SplitRatio;
            if (_settings.SwapSides) left = !left;
            return left ? ShadeKind.Notifications : ShadeKind.QuickSettings;
        }
        return null;
    }

    private bool HasMonitorAbove(ScreenRect m, int x)
        => _monitors.Any(o => o != m && o.Bottom == m.Top && x >= o.Left && x < o.Right);

    /// <summary>Движение мыши. Координаты — те, что пришли в хук (могут быть за краем экрана).</summary>
    public void OnMouseMove(int x, int y, long timestampMs, bool buttonDown = false)
    {
        if (!_settings.MouseEnabled) return;
        var zone = ZoneAt(x, ClampToMonitorTop(x, y));
        if (zone is null)
        {
            if (_armedZone is not null) HintChanged?.Invoke(this, null);
            Reset();
            _firedForCurrentStay = false;
            return;
        }
        if (Suppressed(timestampMs, buttonDown) || _firedForCurrentStay) return;

        if (_armedZone != zone)
        {
            _armedZone = zone;
            _armedSince = timestampMs;
            _pushCount = 0;
            HintChanged?.Invoke(this, zone);
        }
        else
        {
            // Повторное событие у края = пользователь «давит» мышью вверх.
            _pushCount++;
        }

        if (_settings.PushEvents > 0 && _pushCount >= _settings.PushEvents)
            Fire(zone.Value, ShadeTrigger.MouseEdgePush, x);
    }

    /// <summary>Вызывать по таймеру (~30 раз в секунду), чтобы сработала задержка без новых движений.</summary>
    public void OnTick(long timestampMs, int cursorX, int cursorY, bool buttonDown = false)
    {
        if (_armedZone is null || _settings.DwellMs <= 0 || _firedForCurrentStay) return;
        if (Suppressed(timestampMs, buttonDown)) return;
        if (ZoneAt(cursorX, ClampToMonitorTop(cursorX, cursorY)) != _armedZone) return;
        if (timestampMs - _armedSince >= _settings.DwellMs)
            Fire(_armedZone.Value, ShadeTrigger.MouseEdgeDwell, cursorX);
    }

    /// <summary>Прокрутка колесом или двумя пальцами по тачпаду.</summary>
    public void OnWheel(int x, int y, int delta, long timestampMs, bool buttonDown = false)
    {
        if (!_settings.TouchpadPullEnabled) return;
        var zone = ZoneAt(x, ClampToMonitorTop(x, y));
        if (zone is null || Suppressed(timestampMs, buttonDown)) { _wheelAccumulator = 0; return; }
        bool accepted = _settings.WheelDirection switch
        {
            WheelPullDirection.Positive => delta > 0,
            WheelPullDirection.Negative => delta < 0,
            _ => delta != 0,
        };
        if (!accepted) { _wheelAccumulator = 0; return; }
        if (timestampMs - _lastWheelTs > 400) _wheelAccumulator = 0;
        _lastWheelTs = timestampMs;
        _wheelAccumulator += Math.Abs(delta);
        if (_wheelAccumulator >= _settings.WheelPullThreshold)
        {
            _wheelAccumulator = 0;
            Fire(zone.Value, ShadeTrigger.TouchpadPull, x);
        }
    }

    private int ClampToMonitorTop(int x, int y)
    {
        // Хук может прислать y меньше верхней границы, когда мышь «упирается» в край.
        foreach (var m in _monitors)
            if (x >= m.Left && x < m.Right && y < m.Top && !HasMonitorAbove(m, x)) return m.Top;
        return y;
    }

    private void Fire(ShadeKind kind, ShadeTrigger trigger, int x)
    {
        _firedForCurrentStay = true;
        IsShadeOpen = true;
        Reset(keepFired: true);
        HintChanged?.Invoke(this, null);
        Triggered?.Invoke(this, new ShadeRequest(kind, trigger, x));
    }

    private void Reset(bool keepFired = false)
    {
        _armedZone = null;
        _pushCount = 0;
        _wheelAccumulator = 0;
        if (!keepFired) _firedForCurrentStay = false;
    }
}
