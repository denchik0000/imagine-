using Imagine.Core.Theming;

namespace Imagine.Core.Services;

/// <summary>
/// Какие значения HKCU меняются при применении темы. Логика отделена от Win32,
/// поэтому её можно проверить тестами на <see cref="InMemoryRegistry"/>.
/// </summary>
public static class WindowsAccentWriter
{
    public const string AccentKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";
    public const string DwmKey = @"Software\Microsoft\Windows\DWM";
    public const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    public const string DesktopKey = @"Control Panel\Desktop";

    public sealed record Options(bool ShowOnTaskbarAndStart, bool ShowOnTitleBars, bool? AppsDark, bool? SystemDark, bool? Transparency);

    public static void Apply(RegistryTransaction tx, WindowsAccentPalette palette, Options options)
    {
        ArgumentNullException.ThrowIfNull(tx);
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(options);

        // Выключаем «автоматически выбирать акцент по фону», иначе Windows перезапишет наш цвет.
        tx.Set(DesktopKey, "AutoColorization", 0, RegistryValueKind.DWord);

        tx.Set(AccentKey, "AccentPalette", palette.ToRegistryBlob(), RegistryValueKind.Binary);
        tx.Set(AccentKey, "AccentColorMenu", unchecked((int)palette.AccentColorMenu), RegistryValueKind.DWord);
        tx.Set(AccentKey, "StartColorMenu", unchecked((int)palette.StartColorMenu), RegistryValueKind.DWord);

        tx.Set(DwmKey, "AccentColor", unchecked((int)palette.AccentColorMenu), RegistryValueKind.DWord);
        tx.Set(DwmKey, "ColorizationColor", unchecked((int)palette.ColorizationColor), RegistryValueKind.DWord);
        tx.Set(DwmKey, "ColorizationAfterglow", unchecked((int)palette.ColorizationColor), RegistryValueKind.DWord);
        tx.Set(DwmKey, "ColorPrevalence", options.ShowOnTitleBars ? 1 : 0, RegistryValueKind.DWord);

        tx.Set(PersonalizeKey, "ColorPrevalence", options.ShowOnTaskbarAndStart ? 1 : 0, RegistryValueKind.DWord);
        if (options.AppsDark is { } apps) tx.Set(PersonalizeKey, "AppsUseLightTheme", apps ? 0 : 1, RegistryValueKind.DWord);
        if (options.SystemDark is { } system) tx.Set(PersonalizeKey, "SystemUsesLightTheme", system ? 0 : 1, RegistryValueKind.DWord);
        if (options.Transparency is { } t) tx.Set(PersonalizeKey, "EnableTransparency", t ? 1 : 0, RegistryValueKind.DWord);
    }

    /// <summary>Акцент на панели задач в Windows 11 виден только при тёмном режиме системы.</summary>
    public static bool TaskbarAccentNeedsDarkSystem => true;
}
