using System.Runtime.InteropServices;
using Imagine.Core.Services;
using Imagine.Core.Settings;
using Imagine.Core.Shades;
using Imagine.Platform.Interop;

namespace Imagine.Platform.Services;

/// <summary>Обои через COM IDesktopWallpaper: свои картинки на каждом мониторе и слайд-шоу.</summary>
public sealed class DesktopWallpaperService : IWallpaperService
{
    private static IDesktopWallpaper Create() => (IDesktopWallpaper)new DesktopWallpaperClass();

    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var dw = Create();
        try
        {
            var list = new List<MonitorInfo>();
            uint count = dw.GetMonitorDevicePathCount();
            for (uint i = 0; i < count; i++)
            {
                string id = dw.GetMonitorDevicePathAt(i);
                NativeMethods.RECT r;
                try { r = dw.GetMonitorRECT(id); }
                catch (COMException) { continue; } // монитор отключён
                bool primary = r.Left == 0 && r.Top == 0;
                list.Add(new MonitorInfo(id, $"Монитор {i + 1} ({r.Right - r.Left}×{r.Bottom - r.Top})", new ScreenRect(r.Left, r.Top, r.Right, r.Bottom), primary));
            }
            return list;
        }
        finally
        {
            Marshal.ReleaseComObject(dw);
        }
    }

    public string? GetWallpaper(string? monitorDevicePath = null)
    {
        var dw = Create();
        try
        {
            string? id = monitorDevicePath ?? (dw.GetMonitorDevicePathCount() > 0 ? dw.GetMonitorDevicePathAt(0) : null);
            var path = dw.GetWallpaper(id);
            return string.IsNullOrEmpty(path) ? null : path;
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            Marshal.ReleaseComObject(dw);
        }
    }

    public void SetWallpaper(string imagePath, string? monitorDevicePath = null, WallpaperFit fit = WallpaperFit.Fill)
    {
        var dw = Create();
        try
        {
            dw.SetPosition(fit switch
            {
                WallpaperFit.Fit => DesktopWallpaperPosition.Fit,
                WallpaperFit.Stretch => DesktopWallpaperPosition.Stretch,
                WallpaperFit.Tile => DesktopWallpaperPosition.Tile,
                WallpaperFit.Center => DesktopWallpaperPosition.Center,
                WallpaperFit.Span => DesktopWallpaperPosition.Span,
                _ => DesktopWallpaperPosition.Fill,
            });
            dw.SetWallpaper(monitorDevicePath, imagePath);
        }
        finally
        {
            Marshal.ReleaseComObject(dw);
        }
    }

    public void SetSlideshow(string folder, TimeSpan interval, bool shuffle)
    {
        var dw = Create();
        var array = ShellItems.FolderAsArray(folder);
        try
        {
            dw.SetSlideshow(array);
            dw.SetSlideshowOptions(shuffle ? DesktopSlideshowOptions.ShuffleImages : DesktopSlideshowOptions.None, (uint)interval.TotalMilliseconds);
        }
        finally
        {
            Marshal.Release(array);
            Marshal.ReleaseComObject(dw);
        }
    }

    public void Advance()
    {
        var dw = Create();
        try { dw.AdvanceSlideshow(null, 0); }
        finally { Marshal.ReleaseComObject(dw); }
    }
}
