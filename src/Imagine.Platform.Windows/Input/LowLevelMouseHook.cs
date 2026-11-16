using System.Runtime.InteropServices;
using Imagine.Platform.Interop;

namespace Imagine.Platform.Input;

/// <summary>Сырые события мыши из WH_MOUSE_LL.</summary>
public readonly record struct MouseHookEvent(int X, int Y, int WheelDelta, bool IsWheel, bool LeftButtonDown, long Timestamp);

/// <summary>
/// Глобальный хук мыши. Устанавливать на потоке с циклом сообщений (UI-поток).
/// Обработчик должен быть быстрым: Windows снимает хук, если он отвечает дольше ~300 мс.
/// </summary>
public sealed class LowLevelMouseHook : IDisposable
{
    private readonly NativeMethods.HookProc _proc;
    private IntPtr _hook;
    private bool _leftDown;

    public LowLevelMouseHook() => _proc = HookCallback;

    public event Action<MouseHookEvent>? Event;

    public bool IsInstalled => _hook != IntPtr.Zero;

    public void Install()
    {
        if (_hook != IntPtr.Zero) return;
        _hook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _proc, NativeMethods.GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    public void Uninstall()
    {
        if (_hook == IntPtr.Zero) return;
        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            if (msg == NativeMethods.WM_LBUTTONDOWN) _leftDown = true;
            else if (msg == NativeMethods.WM_LBUTTONUP) _leftDown = false;
            if (msg is NativeMethods.WM_MOUSEMOVE or NativeMethods.WM_MOUSEWHEEL)
            {
                bool wheel = msg == NativeMethods.WM_MOUSEWHEEL;
                int delta = wheel ? (short)((data.mouseData >> 16) & 0xFFFF) : 0;
                try
                {
                    Event?.Invoke(new MouseHookEvent(data.pt.X, data.pt.Y, delta, wheel, _leftDown, NativeMethods.Now()));
                }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
                {
                    // Хук не должен падать.
                }
            }
        }
        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();
}
