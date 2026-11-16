using Imagine.Platform.Interop;

namespace Imagine.Platform.Windowing;

/// <summary>
/// Встраивание окна в рабочий стол «под иконки» (приём Lively Wallpaper с WorkerW).
/// Работает и на 24H2, где WorkerW стал дочерним окном Progman. Если не вышло — окно просто остаётся снизу.
/// </summary>
public static class DesktopHost
{
    private const uint SpawnWorkerW = 0x052C;

    public static bool TryAttach(IntPtr hwnd)
    {
        var progman = NativeMethods.FindWindow("Progman", null);
        if (progman == IntPtr.Zero) return false;
        NativeMethods.SendMessageTimeout(progman, SpawnWorkerW, new IntPtr(0xD), new IntPtr(0x1), NativeMethods.SMTO_ABORTIFHUNG, 1000, out _);

        // 24H2+: WorkerW — дочернее окно Progman.
        var worker = NativeMethods.FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
        if (worker == IntPtr.Zero)
        {
            // Классический путь: WorkerW — соседнее окно после того, в котором SHELLDLL_DefView.
            NativeMethods.EnumWindows((top, _) =>
            {
                var defView = NativeMethods.FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (defView != IntPtr.Zero) worker = NativeMethods.FindWindowEx(IntPtr.Zero, top, "WorkerW", null);
                return worker == IntPtr.Zero;
            }, IntPtr.Zero);
        }
        if (worker == IntPtr.Zero) return false;

        long style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE).ToInt64();
        style = (style & ~NativeMethods.WS_POPUP) | NativeMethods.WS_CHILD;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE, new IntPtr(style));
        return NativeMethods.SetParent(hwnd, worker) != IntPtr.Zero;
    }
}
