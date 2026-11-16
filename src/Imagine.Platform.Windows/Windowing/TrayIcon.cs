using System.Runtime.InteropServices;

namespace Imagine.Platform.Windowing;

/// <summary>Пункт меню значка в трее. Пустой текст — разделитель.</summary>
public sealed record TrayMenuItem(string Text, Action? Action = null, bool Checked = false, bool Enabled = true)
{
    public static TrayMenuItem Separator { get; } = new("");
}

/// <summary>
/// Значок в области уведомлений на чистом Shell_NotifyIcon: не зависит от XAML-библиотек,
/// поэтому работает и в упакованной, и в портативной версии.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private const int WM_APP_TRAY = 0x8000 + 42;
    private const int NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2, NIM_SETVERSION = 4;
    private const int NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4, NIF_SHOWTIP = 0x80;
    private const int NOTIFYICON_VERSION_4 = 4;
    private const int WM_LBUTTONUP = 0x0202, WM_RBUTTONUP = 0x0205, WM_CONTEXTMENU = 0x007B, NIN_SELECT = 0x0400, NIN_KEYSELECT = 0x0401;
    private const uint TPM_RETURNCMD = 0x0100, TPM_RIGHTBUTTON = 0x0002, MF_STRING = 0, MF_SEPARATOR = 0x800, MF_CHECKED = 0x8, MF_GRAYED = 0x1;
    private const uint IMAGE_ICON = 1, LR_LOADFROMFILE = 0x10, LR_DEFAULTSIZE = 0x40;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public int uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(int message, ref NOTIFYICONDATA data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr hinst, string name, uint type, int cx, int cy, uint load);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr id, string? text);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr hwnd, IntPtr tpm);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr menu);

    private readonly Input.MessageWindow _window;
    private readonly Func<IReadOnlyList<TrayMenuItem>> _menu;
    private readonly IntPtr _icon;
    private bool _added;

    public TrayIcon(string iconPath, string tooltip, Func<IReadOnlyList<TrayMenuItem>> menu)
    {
        _window = new Input.MessageWindow();
        _window.RawMessage += OnMessage;
        _menu = menu;
        _icon = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE);
        var data = Create(tooltip);
        _added = Shell_NotifyIcon(NIM_ADD, ref data);
        data.uVersion = NOTIFYICON_VERSION_4;
        Shell_NotifyIcon(NIM_SETVERSION, ref data);
        // После перезапуска Проводника значок нужно добавить заново.
        _window.TaskbarCreated += (_, _) =>
        {
            var d = Create(tooltip);
            _added = Shell_NotifyIcon(NIM_ADD, ref d);
        };
    }

    public event EventHandler? Clicked;

    public void SetTooltip(string text)
    {
        var data = Create(text);
        Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    private NOTIFYICONDATA Create(string tooltip) => new()
    {
        cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = _window.Handle,
        uID = 1,
        uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP,
        uCallbackMessage = WM_APP_TRAY,
        hIcon = _icon,
        szTip = tooltip.Length > 127 ? tooltip[..127] : tooltip,
        szInfo = "",
        szInfoTitle = "",
    };

    private void OnMessage(uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg != WM_APP_TRAY) return;
        int evt = (int)(lParam.ToInt64() & 0xFFFF);
        switch (evt)
        {
            case WM_LBUTTONUP:
            case NIN_SELECT:
            case NIN_KEYSELECT:
                Clicked?.Invoke(this, EventArgs.Empty);
                break;
            case WM_RBUTTONUP:
            case WM_CONTEXTMENU:
                ShowMenu();
                break;
        }
    }

    private void ShowMenu()
    {
        var items = _menu();
        var menu = CreatePopupMenu();
        try
        {
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it.Text.Length == 0) AppendMenu(menu, MF_SEPARATOR, UIntPtr.Zero, null);
                else AppendMenu(menu, MF_STRING | (it.Checked ? MF_CHECKED : 0) | (it.Enabled ? 0 : MF_GRAYED), (UIntPtr)(i + 1), it.Text);
            }
            var (x, y) = Monitors.CursorPosition();
            Interop.NativeMethods.SetForegroundWindow(_window.Handle);
            int cmd = TrackPopupMenuEx(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON, x, y, _window.Handle, IntPtr.Zero);
            if (cmd > 0 && cmd <= items.Count) items[cmd - 1].Action?.Invoke();
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    public void Dispose()
    {
        if (_added)
        {
            var data = Create("");
            Shell_NotifyIcon(NIM_DELETE, ref data);
            _added = false;
        }
        if (_icon != IntPtr.Zero) DestroyIcon(_icon);
        _window.Dispose();
    }
}
