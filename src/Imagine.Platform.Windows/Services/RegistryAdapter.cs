using Imagine.Core.Services;
using Microsoft.Win32;
using CoreKind = Imagine.Core.Services.RegistryValueKind;

namespace Imagine.Platform.Services;

/// <summary>HKEY_CURRENT_USER через Microsoft.Win32.Registry.</summary>
public sealed class RegistryAdapter : IRegistry
{
    public object? GetValue(string keyPath, string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: false);
        return key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
    }

    public void SetValue(string keyPath, string name, object value, CoreKind kind)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
        key.SetValue(name, value, kind switch
        {
            CoreKind.DWord => Microsoft.Win32.RegistryValueKind.DWord,
            CoreKind.Binary => Microsoft.Win32.RegistryValueKind.Binary,
            CoreKind.ExpandString => Microsoft.Win32.RegistryValueKind.ExpandString,
            _ => Microsoft.Win32.RegistryValueKind.String,
        });
    }

    public void DeleteValue(string keyPath, string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }
}
