using System.Diagnostics;
using System.Runtime.InteropServices;
using Imagine.Core.Services;
using Imagine.Platform.Interop;

namespace Imagine.Platform.Services;

public sealed class SystemActions : ISystemActions
{
    private const ushort VK_LWIN = 0x5B, VK_V = 0x56, VK_OEM_PERIOD = 0xBE;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public static void Open(string uri)
    {
        try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true })?.Dispose(); }
        catch (System.ComponentModel.Win32Exception) { }
    }

    public void OpenUri(string uri) => Open(uri);

    public void TakeScreenshot() => Open("ms-screenclip:");

    public void LockWorkstation() => NativeMethods.LockWorkStation();

    public void OpenClipboardHistory() => SendChord(VK_LWIN, VK_V);

    public void OpenEmojiPanel() => SendChord(VK_LWIN, VK_OEM_PERIOD);

    private static void SendChord(ushort modifier, ushort key)
    {
        NativeMethods.INPUT Key(ushort vk, bool up) => new() { type = 1, ki = new NativeMethods.KEYBDINPUT { wVk = vk, dwFlags = up ? KEYEVENTF_KEYUP : 0 } };
        var inputs = new[] { Key(modifier, false), Key(key, false), Key(key, true), Key(modifier, true) };
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }
}
