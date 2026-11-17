using System.Runtime.InteropServices;

namespace Imagine.Platform.Interop;

internal enum DesktopWallpaperPosition
{
    Center = 0,
    Tile = 1,
    Stretch = 2,
    Fit = 3,
    Fill = 4,
    Span = 5,
}

[Flags]
internal enum DesktopSlideshowOptions { None = 0, ShuffleImages = 1 }

/// <summary>IDesktopWallpaper (shobjidl_core.h). Порядок методов важен: это vtable.</summary>
[ComImport]
[Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDesktopWallpaper
{
    void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorId, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);

    [return: MarshalAs(UnmanagedType.LPWStr)]
    string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorId);

    [return: MarshalAs(UnmanagedType.LPWStr)]
    string GetMonitorDevicePathAt(uint monitorIndex);

    [return: MarshalAs(UnmanagedType.U4)]
    uint GetMonitorDevicePathCount();

    NativeMethods.RECT GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorId);

    void SetBackgroundColor([MarshalAs(UnmanagedType.U4)] uint color);

    [return: MarshalAs(UnmanagedType.U4)]
    uint GetBackgroundColor();

    void SetPosition([MarshalAs(UnmanagedType.I4)] DesktopWallpaperPosition position);

    [return: MarshalAs(UnmanagedType.I4)]
    DesktopWallpaperPosition GetPosition();

    void SetSlideshow(IntPtr items);

    IntPtr GetSlideshow();

    void SetSlideshowOptions(DesktopSlideshowOptions options, uint slideshowTick);

    void GetSlideshowOptions(out DesktopSlideshowOptions options, out uint slideshowTick);

    void AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)] string? monitorId, [MarshalAs(UnmanagedType.I4)] int direction);

    int GetStatus();

    void Enable([MarshalAs(UnmanagedType.Bool)] bool enable);
}

[ComImport]
[Guid("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD")]
internal class DesktopWallpaperClass
{
}

internal static class ShellItems
{
    private static readonly Guid IID_IShellItem = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(string path, IntPtr pbc, in Guid riid, out IntPtr ppv);

    [DllImport("shell32.dll", PreserveSig = false)]
    private static extern void SHCreateShellItemArrayFromShellItem(IntPtr psi, in Guid riid, out IntPtr ppv);

    private static readonly Guid IID_IShellItemArray = new("b63ea76d-1f85-456f-a19c-48159efa858b");

    /// <summary>IShellItemArray из одной папки (для слайд-шоу). Вызывающий освобождает через Marshal.Release.</summary>
    public static IntPtr FolderAsArray(string folder)
    {
        SHCreateItemFromParsingName(folder, IntPtr.Zero, IID_IShellItem, out var item);
        try
        {
            SHCreateShellItemArrayFromShellItem(item, IID_IShellItemArray, out var array);
            return array;
        }
        finally
        {
            Marshal.Release(item);
        }
    }
}
