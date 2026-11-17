using System.Management;
using Imagine.Core.Services;
using Imagine.Platform.Interop;

namespace Imagine.Platform.Services;

/// <summary>Яркость: встроенный экран ноутбука через WMI, внешние мониторы через DDC/CI (как Twinkle Tray).</summary>
public sealed class BrightnessService : IBrightnessService
{
    private const string InternalId = "internal";

    public IReadOnlyList<DisplayBrightness> GetDisplays()
    {
        var list = new List<DisplayBrightness>();
        if (GetInternal() is int internalPercent) list.Add(new DisplayBrightness(InternalId, "Встроенный экран", internalPercent, true));
        int index = 0;
        foreach (var (handle, name) in EnumeratePhysical())
        {
            try
            {
                if (NativeMethods.GetMonitorBrightness(handle, out uint min, out uint cur, out uint max) && max > min)
                    list.Add(new DisplayBrightness($"ddc:{index}", string.IsNullOrWhiteSpace(name) ? $"Монитор {index + 1}" : name, (int)Math.Round(100.0 * (cur - min) / (max - min)), false));
            }
            finally
            {
                index++;
            }
        }
        DestroyAll();
        return list;
    }

    public bool SetBrightness(string monitorId, int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        if (monitorId == InternalId) return SetInternal(percent);
        if (!monitorId.StartsWith("ddc:", StringComparison.Ordinal) || !int.TryParse(monitorId[4..], out int target)) return false;
        int index = 0;
        bool ok = false;
        foreach (var (handle, _) in EnumeratePhysical())
        {
            if (index++ != target) continue;
            if (NativeMethods.GetMonitorBrightness(handle, out uint min, out _, out uint max))
                ok = NativeMethods.SetMonitorBrightness(handle, (uint)(min + (max - min) * percent / 100.0));
        }
        DestroyAll();
        return ok;
    }

    private static int? GetInternal()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(new ManagementScope(@"\\.\root\wmi"), new SelectQuery("WmiMonitorBrightness"));
            foreach (ManagementObject o in searcher.Get())
                using (o) return Convert.ToInt32(o["CurrentBrightness"], System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (ManagementException) { }
        catch (UnauthorizedAccessException) { }
        return null;
    }

    private static bool SetInternal(int percent)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(new ManagementScope(@"\\.\root\wmi"), new SelectQuery("WmiMonitorBrightnessMethods"));
            foreach (ManagementObject o in searcher.Get())
            {
                using (o) o.InvokeMethod("WmiSetBrightness", [1u, (byte)percent]);
                return true;
            }
        }
        catch (ManagementException) { }
        catch (UnauthorizedAccessException) { }
        return false;
    }

    private readonly List<NativeMethods.PHYSICAL_MONITOR[]> _toDestroy = [];

    private IEnumerable<(IntPtr Handle, string Name)> EnumeratePhysical()
    {
        var monitors = new List<IntPtr>();
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr _, ref NativeMethods.RECT _, IntPtr _) => { monitors.Add(h); return true; }, IntPtr.Zero);
        foreach (var hMon in monitors)
        {
            if (!NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out uint count) || count == 0) continue;
            var arr = new NativeMethods.PHYSICAL_MONITOR[count];
            if (!NativeMethods.GetPhysicalMonitorsFromHMONITOR(hMon, count, arr)) continue;
            _toDestroy.Add(arr);
            foreach (var pm in arr) yield return (pm.hPhysicalMonitor, pm.szPhysicalMonitorDescription);
        }
    }

    private void DestroyAll()
    {
        foreach (var arr in _toDestroy) NativeMethods.DestroyPhysicalMonitors((uint)arr.Length, arr);
        _toDestroy.Clear();
    }
}
