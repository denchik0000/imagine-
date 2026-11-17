using Imagine.Core.Services;
using Imagine.Core.Theming;
using Imagine.Platform.Interop;

namespace Imagine.Platform.Services;

/// <summary>Акцент и режим Windows через HKCU + широковещательное WM_SETTINGCHANGE (без прав администратора).</summary>
public sealed class AccentService(IRegistry registry) : IAccentService
{
    public WindowsAccentPalette? ReadCurrent()
        => registry.GetValue(WindowsAccentWriter.AccentKey, "AccentPalette") is byte[] { Length: >= 32 } blob
            ? WindowsAccentPalette.FromRegistryBlob(blob)
            : null;

    public void Apply(WindowsAccentPalette palette, bool showOnTaskbarAndStart, bool showOnTitleBars)
    {
        var tx = new RegistryTransaction(registry);
        WindowsAccentWriter.Apply(tx, palette, new WindowsAccentWriter.Options(showOnTaskbarAndStart, showOnTitleBars, null, null, null));
        Broadcast();
    }

    public void SetThemeMode(bool appsDark, bool systemDark)
    {
        registry.SetValue(WindowsAccentWriter.PersonalizeKey, "AppsUseLightTheme", appsDark ? 0 : 1, RegistryValueKind.DWord);
        registry.SetValue(WindowsAccentWriter.PersonalizeKey, "SystemUsesLightTheme", systemDark ? 0 : 1, RegistryValueKind.DWord);
        Broadcast();
    }

    public void SetTransparency(bool enabled)
    {
        registry.SetValue(WindowsAccentWriter.PersonalizeKey, "EnableTransparency", enabled ? 1 : 0, RegistryValueKind.DWord);
        Broadcast();
    }

    /// <summary>
    /// Проводник перечитывает акцент по «ImmersiveColorSet», тему — по «WindowsThemeElement».
    /// Панель задач иногда пропускает первое сообщение, поэтому шлём дважды с паузой.
    /// </summary>
    public void Broadcast()
    {
        Send("ImmersiveColorSet");
        Send("WindowsThemeElement");
        _ = Task.Delay(600).ContinueWith(_ => Send("ImmersiveColorSet"), TaskScheduler.Default);
    }

    private static void Send(string area)
        => NativeMethods.SendMessageTimeout(NativeMethods.HWND_BROADCAST, NativeMethods.WM_SETTINGCHANGE, IntPtr.Zero, area,
            NativeMethods.SMTO_ABORTIFHUNG, 1000, out _);
}
