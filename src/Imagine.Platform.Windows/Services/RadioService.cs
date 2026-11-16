using Imagine.Core.Services;
using Windows.Devices.Radios;
using Windows.Devices.WiFi;

namespace Imagine.Platform.Services;

/// <summary>Wi‑Fi, Bluetooth и режим «В самолёте» через Windows.Devices.Radios (без прав администратора).</summary>
public sealed class RadioService : IRadioService
{
    private IReadOnlyList<Radio>? _radios;
    private bool _accessRequested;

    public event EventHandler? Changed;

    private async Task<IReadOnlyList<Radio>> RadiosAsync()
    {
        if (!_accessRequested)
        {
            _accessRequested = true;
            await Radio.RequestAccessAsync();
        }
        if (_radios is not null) return _radios;
        var radios = await Radio.GetRadiosAsync();
        foreach (var r in radios) r.StateChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        _radios = radios.ToList();
        return _radios;
    }

    private async Task<Radio?> FindAsync(RadioKind kind) => (await RadiosAsync().ConfigureAwait(false)).FirstOrDefault(r => r.Kind == kind);

    public async Task<bool?> GetWifiStateAsync() => (await FindAsync(RadioKind.WiFi).ConfigureAwait(false))?.State == RadioState.On;

    public async Task<bool?> GetBluetoothStateAsync()
    {
        var r = await FindAsync(RadioKind.Bluetooth).ConfigureAwait(false);
        return r is null ? null : r.State == RadioState.On;
    }

    public Task<bool> SetWifiAsync(bool on) => SetAsync(RadioKind.WiFi, on);

    public Task<bool> SetBluetoothAsync(bool on) => SetAsync(RadioKind.Bluetooth, on);

    /// <summary>Системного флага «В самолёте» в публичном API нет, поэтому выключаем все радиомодули сами.</summary>
    public async Task<bool> SetAirplaneModeAsync(bool on)
    {
        bool ok = true;
        foreach (var r in await RadiosAsync().ConfigureAwait(false))
        {
            var result = await r.SetStateAsync(on ? RadioState.Off : RadioState.On);
            ok &= result == RadioAccessStatus.Allowed;
        }
        return ok;
    }

    private async Task<bool> SetAsync(RadioKind kind, bool on)
    {
        var r = await FindAsync(kind).ConfigureAwait(false);
        if (r is null) return false;
        var result = await r.SetStateAsync(on ? RadioState.On : RadioState.Off);
        return result == RadioAccessStatus.Allowed;
    }

    /// <summary>
    /// Список сетей. С осени 2024 Windows требует разрешения на геолокацию для приложений,
    /// которые сканируют Wi‑Fi, иначе вернётся пустой список.
    /// </summary>
    public async Task<IReadOnlyList<WifiNetwork>> ScanWifiAsync(CancellationToken ct = default)
    {
        var access = await WiFiAdapter.RequestAccessAsync();
        if (access != WiFiAccessStatus.Allowed) return [];
        var adapters = await WiFiAdapter.FindAllAdaptersAsync();
        var adapter = adapters.FirstOrDefault();
        if (adapter is null) return [];
        await adapter.ScanAsync().AsTask(ct).ConfigureAwait(false);
        string? connected = null;
        try
        {
            var profile = await adapter.NetworkAdapter.GetConnectedProfileAsync();
            connected = profile?.ProfileName;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException) { }
        return adapter.NetworkReport.AvailableNetworks
            .Where(n => !string.IsNullOrEmpty(n.Ssid))
            .GroupBy(n => n.Ssid)
            .Select(g => g.OrderByDescending(n => n.SignalBars).First())
            .Select(n => new WifiNetwork(n.Ssid, n.SignalBars, n.SecuritySettings.NetworkAuthenticationType != Windows.Networking.Connectivity.NetworkAuthenticationType.Open80211, n.Ssid == connected))
            .OrderByDescending(n => n.IsConnected)
            .ThenByDescending(n => n.SignalBars)
            .ToList();
    }
}
