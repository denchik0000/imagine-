using System.Runtime.InteropServices;
using Imagine.Core.Shades;
using Imagine.Platform.Interop;

namespace Imagine.Platform.Windowing;

public sealed record DisplayArea(IntPtr Handle, string DeviceName, ScreenRect Bounds, ScreenRect WorkArea, bool IsPrimary);

/// <summary>Мониторы в физических пикселях.</summary>
public static class Monitors
{
    private const uint MONITORINFOF_PRIMARY = 1;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    public static IReadOnlyList<DisplayArea> All()
    {
        var list = new List<DisplayArea>();
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr _, ref NativeMethods.RECT _, IntPtr _) =>
        {
            if (Get(h) is { } area) list.Add(area);
            return true;
        }, IntPtr.Zero);
        return list;
    }

    public static DisplayArea? FromPoint(int x, int y)
        => Get(NativeMethods.MonitorFromPoint(new NativeMethods.POINT { X = x, Y = y }, MONITOR_DEFAULTTONEAREST));

    public static DisplayArea? Primary() => All().FirstOrDefault(a => a.IsPrimary);

    public static (int X, int Y) CursorPosition()
    {
        NativeMethods.GetCursorPos(out var p);
        return (p.X, p.Y);
    }

    private static DisplayArea? Get(IntPtr hMonitor)
    {
        var info = new NativeMethods.MONITORINFOEX { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
        if (!NativeMethods.GetMonitorInfo(hMonitor, ref info)) return null;
        var m = info.rcMonitor;
        var w = info.rcWork;
        return new DisplayArea(hMonitor, info.szDevice, new ScreenRect(m.Left, m.Top, m.Right, m.Bottom), new ScreenRect(w.Left, w.Top, w.Right, w.Bottom), (info.dwFlags & MONITORINFOF_PRIMARY) != 0);
    }
}
