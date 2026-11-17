using System.Runtime.InteropServices;
using Imagine.Core.Hotkeys;
using Imagine.Platform.Interop;

namespace Imagine.Platform.Input;

/// <summary>
/// Невидимое окно сообщений: глобальные горячие клавиши (RegisterHotKey), смена настроек и дисплеев,
/// блокировка/разблокировка сеанса (для «переезда» часов).
/// </summary>
public sealed class MessageWindow : IDisposable
{
    private static int s_classCounter;

    // Живые окна держим статически: пока окно существует, Windows может вызвать его WndProc,
    // и делегат не должен попасть под сборку мусора, даже если на объект никто больше не ссылается.
    private static readonly HashSet<MessageWindow> s_alive = [];
    private readonly NativeMethods.WndProc _wndProc;
    private readonly Dictionary<int, Action> _hotkeys = [];
    private int _nextId = 1;

    public MessageWindow()
    {
        _wndProc = WndProc;
        string className = "ImagineMessageWindow" + Interlocked.Increment(ref s_classCounter);
        var wc = new NativeMethods.WNDCLASSEX
        {
            cbSize = Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = NativeMethods.GetModuleHandle(null),
            lpszClassName = className,
        };
        NativeMethods.RegisterClassEx(ref wc);
        // Обычное (не message-only) скрытое окно, чтобы получать широковещательные WM_SETTINGCHANGE/WM_DISPLAYCHANGE.
        Handle = NativeMethods.CreateWindowEx(0, className, "Imagine", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, wc.hInstance, IntPtr.Zero);
        NativeMethods.WTSRegisterSessionNotification(Handle, 0);
        lock (s_alive) s_alive.Add(this);
    }

    public IntPtr Handle { get; }

    public event EventHandler<string?>? SettingChanged;
    public event EventHandler? DisplayChanged;
    public event EventHandler? SessionLocked;
    public event EventHandler? SessionUnlocked;

    /// <summary>Любое сообщение окна (для значка в трее).</summary>
    public event Action<uint, IntPtr, IntPtr>? RawMessage;

    /// <summary>Проводник перезапустился (нужно заново добавить значок в трей).</summary>
    public event EventHandler? TaskbarCreated;

    private static readonly uint s_taskbarCreated = RegisterWindowMessage("TaskbarCreated");

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);

    /// <summary>Регистрирует сочетание. false — занято системой или другой программой.</summary>
    public bool RegisterHotkey(HotkeyGesture gesture, Action action)
    {
        if (gesture.IsEmpty) return false;
        uint mods = NativeMethods.MOD_NOREPEAT;
        if (gesture.Modifiers.HasFlag(HotkeyModifiers.Alt)) mods |= NativeMethods.MOD_ALT;
        if (gesture.Modifiers.HasFlag(HotkeyModifiers.Control)) mods |= NativeMethods.MOD_CONTROL;
        if (gesture.Modifiers.HasFlag(HotkeyModifiers.Shift)) mods |= NativeMethods.MOD_SHIFT;
        if (gesture.Modifiers.HasFlag(HotkeyModifiers.Win)) mods |= NativeMethods.MOD_WIN;
        int id = _nextId++;
        if (!NativeMethods.RegisterHotKey(Handle, id, mods, (uint)gesture.VirtualKey)) return false;
        _hotkeys[id] = action;
        return true;
    }

    public void UnregisterAllHotkeys()
    {
        foreach (var id in _hotkeys.Keys) NativeMethods.UnregisterHotKey(Handle, id);
        _hotkeys.Clear();
    }

    /// <summary>Ошибка в обработчике сообщения, которую поймали, чтобы не уронить процесс.</summary>
    public static event Action<Exception>? HandlerFailed;

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            return WndProcCore(hWnd, msg, wParam, lParam);
        }
        catch (Exception ex)
        {
            // Исключение, вылетевшее из оконной процедуры в нативный код, убивает процесс без шансов.
            HandlerFailed?.Invoke(ex);
            return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
        }
    }

    private IntPtr WndProcCore(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        RawMessage?.Invoke(msg, wParam, lParam);
        if (msg == s_taskbarCreated) TaskbarCreated?.Invoke(this, EventArgs.Empty);
        switch (msg)
        {
            case NativeMethods.WM_HOTKEY:
                if (_hotkeys.TryGetValue((int)wParam, out var action)) action();
                return IntPtr.Zero;
            case NativeMethods.WM_SETTINGCHANGE:
                SettingChanged?.Invoke(this, lParam == IntPtr.Zero ? null : Marshal.PtrToStringUni(lParam));
                break;
            case NativeMethods.WM_DISPLAYCHANGE:
                DisplayChanged?.Invoke(this, EventArgs.Empty);
                break;
            case NativeMethods.WM_WTSSESSION_CHANGE:
                if ((int)wParam == NativeMethods.WTS_SESSION_LOCK) SessionLocked?.Invoke(this, EventArgs.Empty);
                else if ((int)wParam == NativeMethods.WTS_SESSION_UNLOCK) SessionUnlocked?.Invoke(this, EventArgs.Empty);
                break;
        }
        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        UnregisterAllHotkeys();
        NativeMethods.WTSUnRegisterSessionNotification(Handle);
        NativeMethods.DestroyWindow(Handle);
        lock (s_alive) s_alive.Remove(this);
    }
}
