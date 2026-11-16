using Imagine.Color;
using Imagine.Core.Services;
using Imagine.Core.Theming;
using Windows.Devices.Enumeration;
using Windows.Devices.Lights;

namespace Imagine.Platform.Services;

/// <summary>
/// Подсветка клавиатуры и мыши под обои через Windows Dynamic Lighting (HID LampArray).
/// Клавиши получают основной цвет, «служебные» — третичный, как акценты в интерфейсе.
/// </summary>
public sealed class LightingService : ILightingService
{
    private readonly List<LampArray> _arrays = [];
    private bool _initialized;

    public int DeviceCount => _arrays.Count;

    private async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;
        var devices = await DeviceInformation.FindAllAsync(LampArray.GetDeviceSelector());
        foreach (var d in devices)
        {
            var array = await LampArray.FromIdAsync(d.Id);
            if (array is not null) _arrays.Add(array);
        }
    }

    public async Task ApplyAsync(ThemePalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        await InitializeAsync().ConfigureAwait(false);
        var primary = ToColor(palette.DarkScheme.Get(ColorRole.Primary));
        var tertiary = ToColor(palette.DarkScheme.Get(ColorRole.Tertiary));
        foreach (var array in _arrays)
        {
            array.SetColor(primary);
            var accentKeys = new[]
            {
                Windows.System.VirtualKey.Escape, Windows.System.VirtualKey.Enter, Windows.System.VirtualKey.Space,
                Windows.System.VirtualKey.LeftWindows, Windows.System.VirtualKey.Up, Windows.System.VirtualKey.Down,
                Windows.System.VirtualKey.Left, Windows.System.VirtualKey.Right,
            };
            if (array.SupportsVirtualKeys) array.SetColorsForKeys(accentKeys.Select(_ => tertiary).ToArray(), accentKeys);
        }
    }

    private static Windows.UI.Color ToColor(uint argb)
        => Windows.UI.Color.FromArgb(255, (byte)ColorUtils.RedFromArgb(argb), (byte)ColorUtils.GreenFromArgb(argb), (byte)ColorUtils.BlueFromArgb(argb));
}
