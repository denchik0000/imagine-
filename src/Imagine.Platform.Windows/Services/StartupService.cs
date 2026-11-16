using Microsoft.Win32;

namespace Imagine.Platform.Services;

/// <summary>Автозапуск через HKCU\...\Run (для MSIX используется StartupTask из манифеста).</summary>
public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "Imagine";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(Name) is string;
    }

    public static void SetEnabled(bool enabled, string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled) key.SetValue(Name, $"\"{exePath}\" --background");
        else key.DeleteValue(Name, throwOnMissingValue: false);
    }
}
