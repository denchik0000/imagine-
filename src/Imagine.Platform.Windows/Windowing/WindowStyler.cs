using Imagine.Color;
using Imagine.Platform.Interop;

namespace Imagine.Platform.Windowing;

/// <summary>Стили наших «оверлейных» окон: шторки, островок, часы.</summary>
public static class WindowStyler
{
    /// <summary>Окно-панель: без рамки и заголовка, не в Alt+Tab и не на панели задач.</summary>
    public static void MakeOverlay(IntPtr hwnd, bool topmost, bool noActivate)
    {
        long style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE).ToInt64();
        style &= ~(NativeMethods.WS_CAPTION | NativeMethods.WS_THICKFRAME | NativeMethods.WS_SYSMENU | NativeMethods.WS_MINIMIZEBOX | NativeMethods.WS_MAXIMIZEBOX);
        style |= NativeMethods.WS_POPUP;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE, new IntPtr(style));

        long ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        ex |= NativeMethods.WS_EX_TOOLWINDOW;
        ex &= ~NativeMethods.WS_EX_APPWINDOW;
        if (noActivate) ex |= NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(ex));

        NativeMethods.SetWindowPos(hwnd, topmost ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_NOTOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);
    }

    /// <summary>Пропускать клики насквозь (для часов «только для красоты»).</summary>
    public static void SetClickThrough(IntPtr hwnd, bool enabled)
    {
        long ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        ex = enabled ? ex | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_LAYERED : ex & ~NativeMethods.WS_EX_TRANSPARENT;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(ex));
    }

    public static void SetCorners(IntPtr hwnd, bool rounded)
    {
        int pref = rounded ? NativeMethods.DWMWCP_ROUND : NativeMethods.DWMWCP_DONOTROUND;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
    }

    /// <summary>Цвет рамки окна DWM (Windows 11). null — без рамки.</summary>
    public static void SetBorderColor(IntPtr hwnd, uint? argb)
    {
        int value = argb is { } c
            ? (ColorUtils.BlueFromArgb(c) << 16) | (ColorUtils.GreenFromArgb(c) << 8) | ColorUtils.RedFromArgb(c)
            : NativeMethods.DWMWA_COLOR_NONE;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_BORDER_COLOR, ref value, sizeof(int));
    }

    public static void SetDarkFrame(IntPtr hwnd, bool dark)
    {
        int v = dark ? 1 : 0;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref v, sizeof(int));
    }

    public static void BringToFront(IntPtr hwnd, bool activate)
    {
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_SHOWWINDOW | (activate ? 0 : NativeMethods.SWP_NOACTIVATE));
        if (activate) NativeMethods.SetForegroundWindow(hwnd);
    }

    public static void SendToBottom(IntPtr hwnd)
        => NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_BOTTOM, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

    public static void MoveResize(IntPtr hwnd, int x, int y, int width, int height)
        => NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);

    public static double GetScale(IntPtr hwnd)
    {
        uint dpi = NativeMethods.GetDpiForWindow(hwnd);
        return dpi == 0 ? 1.0 : dpi / 96.0;
    }
}
