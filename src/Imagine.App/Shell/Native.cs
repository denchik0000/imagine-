using System.Runtime.InteropServices;

namespace Imagine.App.Shell;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
    [DllImport("gdi32.dll")] public static extern uint GetPixel(IntPtr dc, int x, int y);

    /// <summary>Цвет пикселя экрана в ARGB.</summary>
    public static uint ScreenPixel(int x, int y)
    {
        var dc = GetDC(IntPtr.Zero);
        try
        {
            uint bgr = GetPixel(dc, x, y);
            return 0xFF000000u | ((bgr & 0xFF) << 16) | (bgr & 0xFF00) | ((bgr >> 16) & 0xFF);
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, dc);
        }
    }
}
