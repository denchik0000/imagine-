using System.Runtime.InteropServices;

namespace Imagine.Platform.Input;

/// <summary>
/// Низкоуровневые хуки мыши и клавиатуры в собственном потоке с собственным циклом сообщений.
/// Если хук живёт в потоке интерфейса, любая его заминка (построение страницы, декодирование картинки)
/// задерживает каждое движение мыши во всей системе — отсюда «жёсткие лаги». Здесь поток занят только хуками.
/// </summary>
public sealed class HookThread : IDisposable
{
    private const uint WM_QUIT = 0x0012;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new();
    private uint _threadId;

    public HookThread(bool mouse, LowLevelKeyboardHook? keyboard)
    {
        Mouse = mouse ? new LowLevelMouseHook() : null;
        Keyboard = keyboard;
        _thread = new Thread(Run) { IsBackground = true, Name = "Imagine input hooks", Priority = ThreadPriority.AboveNormal };
        _thread.Start();
        _ready.Wait(TimeSpan.FromSeconds(5));
    }

    public LowLevelMouseHook? Mouse { get; }

    public LowLevelKeyboardHook? Keyboard { get; }

    /// <summary>Ошибка установки хука (например, политика безопасности), если была.</summary>
    public Exception? Error { get; private set; }

    private void Run()
    {
        _threadId = GetCurrentThreadId();
        try
        {
            Mouse?.Install();
            Keyboard?.Install();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Error = ex;
        }
        // Создаём очередь сообщений потока до того, как сообщим о готовности.
        PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
        _ready.Set();
        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
        Mouse?.Uninstall();
        Keyboard?.Uninstall();
    }

    public void Dispose()
    {
        if (_threadId != 0) PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(TimeSpan.FromSeconds(2));
        _ready.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int x, y;
    }

    [DllImport("user32.dll")] private static extern int GetMessage(out MSG msg, IntPtr hwnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out MSG msg, IntPtr hwnd, uint min, uint max, uint remove);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref MSG msg);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint thread, uint msg, IntPtr w, IntPtr l);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
}
