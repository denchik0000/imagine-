using Imagine.Core.Shades;

namespace Imagine.Core.Tests;

public class ShadeTests
{
    private static readonly ScreenRect Main = new(0, 0, 1920, 1080);

    private static (EdgeZoneDetector D, List<ShadeRequest> Fired) Make(EdgeSettings? s = null, params ScreenRect[] monitors)
    {
        var d = new EdgeZoneDetector(s ?? new EdgeSettings(), monitors.Length == 0 ? [Main] : monitors);
        var fired = new List<ShadeRequest>();
        d.Triggered += (_, r) => fired.Add(r);
        return (d, fired);
    }

    [Fact]
    public void Left_half_opens_notifications_right_half_opens_quick_settings()
    {
        var (d, _) = Make();
        Assert.Equal(ShadeKind.Notifications, d.ZoneAt(300, 0));
        Assert.Equal(ShadeKind.QuickSettings, d.ZoneAt(1500, 0));
        Assert.Null(d.ZoneAt(300, 5));
    }

    [Fact]
    public void Dead_zones_protect_window_buttons()
    {
        var (d, _) = Make();
        Assert.Null(d.ZoneAt(1919, 0));
        Assert.Null(d.ZoneAt(1800, 0));
        Assert.Null(d.ZoneAt(5, 0));
    }

    [Fact]
    public void Swap_sides_inverts_zones()
    {
        var (d, _) = Make(new EdgeSettings { SwapSides = true });
        Assert.Equal(ShadeKind.QuickSettings, d.ZoneAt(300, 0));
    }

    [Fact]
    public void Dwell_opens_after_delay()
    {
        var (d, fired) = Make(new EdgeSettings { DwellMs = 200, PushEvents = 0 });
        d.OnMouseMove(400, 0, 1000);
        d.OnTick(1100, 400, 0);
        Assert.Empty(fired);
        d.OnTick(1210, 400, 0);
        Assert.Single(fired);
        Assert.Equal(ShadeKind.Notifications, fired[0].Kind);
        Assert.Equal(ShadeTrigger.MouseEdgeDwell, fired[0].Trigger);
    }

    [Fact]
    public void Pushing_into_edge_opens_immediately()
    {
        var (d, fired) = Make(new EdgeSettings { DwellMs = 5000, PushEvents = 3 });
        for (int i = 0; i < 4; i++) d.OnMouseMove(1500, -3, 1000 + i * 8);
        Assert.Single(fired);
        Assert.Equal(ShadeTrigger.MouseEdgePush, fired[0].Trigger);
        Assert.Equal(ShadeKind.QuickSettings, fired[0].Kind);
    }

    [Fact]
    public void Does_not_fire_while_dragging_window_or_in_fullscreen()
    {
        var (d, fired) = Make(new EdgeSettings { DwellMs = 100, PushEvents = 2 });
        for (int i = 0; i < 5; i++) d.OnMouseMove(400, 0, 1000 + i, buttonDown: true);
        d.OnTick(1500, 400, 0, buttonDown: true);
        d.IsFullscreenAppActive = true;
        for (int i = 0; i < 5; i++) d.OnMouseMove(400, 0, 2000 + i);
        Assert.Empty(fired);
    }

    [Fact]
    public void Fires_once_per_stay_and_respects_cooldown()
    {
        var (d, fired) = Make(new EdgeSettings { DwellMs = 100, PushEvents = 0, CooldownMs = 500 });
        d.OnMouseMove(400, 0, 0);
        d.OnTick(150, 400, 0);
        d.OnTick(300, 400, 0);
        Assert.Single(fired);
        d.NotifyClosed(400);
        d.OnMouseMove(400, 300, 450); // ушли от края
        d.OnMouseMove(400, 0, 500);   // вернулись во время паузы
        d.OnTick(700, 400, 0);
        Assert.Single(fired);
        d.OnMouseMove(400, 300, 1000);
        d.OnMouseMove(400, 0, 1100);
        d.OnTick(1250, 400, 0);
        Assert.Equal(2, fired.Count);
    }

    [Fact]
    public void Touchpad_pull_accumulates_wheel()
    {
        var (d, fired) = Make(new EdgeSettings { WheelPullThreshold = 240 });
        d.OnWheel(500, 0, 120, 0);
        Assert.Empty(fired);
        d.OnWheel(500, 0, 120, 50);
        Assert.Single(fired);
        Assert.Equal(ShadeTrigger.TouchpadPull, fired[0].Trigger);
    }

    [Fact]
    public void Wheel_in_wrong_direction_or_slow_does_not_fire()
    {
        var (d, fired) = Make(new EdgeSettings { WheelPullThreshold = 240 });
        d.OnWheel(500, 0, -120, 0);
        d.OnWheel(500, 0, -120, 10);
        d.OnWheel(500, 0, 120, 100);
        d.OnWheel(500, 0, 120, 1000); // пауза больше 400 мс — сначала
        Assert.Empty(fired);
    }

    [Fact]
    public void Lower_monitor_top_edge_is_not_an_edge()
    {
        var upper = new ScreenRect(0, -1080, 1920, 0);
        var (d, _) = Make(null, Main, upper);
        Assert.Null(d.ZoneAt(400, 0));
        Assert.Equal(ShadeKind.Notifications, d.ZoneAt(400, -1080));
    }

    [Fact]
    public void Side_by_side_monitors_each_have_edges()
    {
        var right = new ScreenRect(1920, 0, 3840, 1080);
        var (d, _) = Make(null, Main, right);
        Assert.Equal(ShadeKind.Notifications, d.ZoneAt(2200, 0));
        Assert.Equal(ShadeKind.QuickSettings, d.ZoneAt(3400, 0));
    }

    [Fact]
    public void Drag_tracker_flings_open_and_closed()
    {
        var t = new ShadeDragTracker(600);
        t.Begin(0, 0);
        t.Move(40, 16);
        t.Move(120, 32);
        t.Move(200, 48);
        var (open, v) = t.End();
        Assert.True(open);
        Assert.True(v > 0);

        t.Begin(500, 1000, initialProgress: 1);
        t.Move(450, 1016);
        t.Move(300, 1032);
        t.Move(200, 1048);
        Assert.False(t.End().Open);
    }

    [Fact]
    public void Drag_tracker_slow_release_uses_threshold()
    {
        var t = new ShadeDragTracker(600);
        t.Begin(0, 0);
        t.Move(300, 2000);
        Assert.True(t.End().Open);
        t.Begin(0, 0);
        t.Move(100, 2000);
        Assert.False(t.End().Open);
    }

    [Fact]
    public void Over_drag_is_rubber_banded()
    {
        var t = new ShadeDragTracker(100);
        t.Begin(0, 0);
        double p = t.Move(1000, 10);
        Assert.InRange(p, 1.0, 1.25);
    }

    [Fact]
    public void Tiles_arrange_without_splitting_wide_tiles()
    {
        var layout = new QuickTileLayout(
        [
            new(QuickTileId.Wifi, QuickTileSize.Wide),
            new(QuickTileId.Focus),
            new(QuickTileId.Bluetooth, QuickTileSize.Wide),
            new(QuickTileId.DarkMode),
        ], columns: 3);
        var a = layout.Arrange();
        Assert.Equal((0, 0, 2), (a[0].Row, a[0].Column, a[0].Span));
        Assert.Equal((0, 2), (a[1].Row, a[1].Column));
        Assert.Equal((1, 0, 2), (a[2].Row, a[2].Column, a[2].Span));
        Assert.Equal((1, 2), (a[3].Row, a[3].Column));
        Assert.Equal(2, layout.RowCount);
    }

    [Fact]
    public void Tiles_can_be_moved_added_and_removed()
    {
        var l = QuickTileLayout.Default.Move(QuickTileId.DarkMode, 0);
        Assert.Equal(QuickTileId.DarkMode, l.Slots[0].Id);
        Assert.DoesNotContain(QuickTileId.Calculator, l.Slots.Select(s => s.Id));
        l = l.Add(QuickTileId.Calculator).Add(QuickTileId.Calculator);
        Assert.Single(l.Slots, s => s.Id == QuickTileId.Calculator);
        l = l.Remove(QuickTileId.Calculator).Resize(QuickTileId.Focus, QuickTileSize.Wide);
        Assert.DoesNotContain(QuickTileId.Calculator, l.Slots.Select(s => s.Id));
        Assert.Equal(QuickTileSize.Wide, l.Slots.First(s => s.Id == QuickTileId.Focus).Size);
    }
}
