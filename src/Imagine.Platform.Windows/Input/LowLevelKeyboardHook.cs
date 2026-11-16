using System.Runtime.InteropServices;
using Imagine.Core.Hotkeys;
using Imagine.Platform.Interop;

namespace Imagine.Platform.Input;

/// <summary>
/// Перехват сочетаний, которые нельзя занять через RegisterHotKey (Win+N, Win+A принадлежат системе).
/// Так делает PowerToys. Не работает поверх окон, запущенных от администратора.
/// </summary>
public sealed class LowLevelKeyboardHook : IDisposable
{
    private const int VK_LWIN = 0x5B, VK_RWIN = 0x5C, VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12;
    private const int VK_NONAME = 0xFC;
    private readonly NativeMethods.HookProc _proc;
    private readonly Dictionary<HotkeyGesture, Action> _intercepts = [];
    private IntPtr _hook;
    private bool _swallowWinUp;

    public LowLevelKeyboardHook() => _proc = HookCallback;

    public void Intercept(HotkeyGesture gesture, Action action) => _intercepts[gesture] = action;

    public void Clear() => _intercepts.Clear();

    public void Install()
    {
        if (_hook != IntPtr.Zero) return;
        _hook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _proc, NativeMethods.GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    public void Uninstall()
    {
        if (_hook == IntPtr.Zero) return;
        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private static bool Down(int vk) => (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _intercepts.Count > 0)
        {
            int msg = (int)wParam;
            var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            if (msg is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN)
            {
                var mods = HotkeyModifiers.None;
                if (Down(VK_LWIN) || Down(VK_RWIN)) mods |= HotkeyModifiers.Win;
                if (Down(VK_CONTROL)) mods |= HotkeyModifiers.Control;
                if (Down(VK_MENU)) mods |= HotkeyModifiers.Alt;
                if (Down(VK_SHIFT)) mods |= HotkeyModifiers.Shift;
                if (_intercepts.TryGetValue(new HotkeyGesture(mods, (int)data.vkCode), out var action))
                {
                    _swallowWinUp = mods.HasFlag(HotkeyModifiers.Win);
                    action();
                    return new IntPtr(1);
                }
            }
            else if (msg is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP && _swallowWinUp && data.vkCode is VK_LWIN or VK_RWIN)
            {
                // Чтобы отпускание Win не открыло меню «Пуск», подмешиваем «пустую» клавишу.
                _swallowWinUp = false;
                var inputs = new[]
                {
                    new NativeMethods.INPUT { type = 1, ki = new NativeMethods.KEYBDINPUT { wVk = VK_NONAME } },
                    new NativeMethods.INPUT { type = 1, ki = new NativeMethods.KEYBDINPUT { wVk = VK_NONAME, dwFlags = 2 } },
                };
                NativeMethods.SendInput(2, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
            }
        }
        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();
}
