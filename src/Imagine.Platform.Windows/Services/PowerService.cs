using Imagine.Core.Services;
using Imagine.Platform.Interop;
using Windows.System.Power;

namespace Imagine.Platform.Services;

/// <summary>Батарея, экономия заряда и режим «не засыпать».</summary>
public sealed class PowerService : IPowerService
{
    public bool IsKeepAwake { get; private set; }

    public BatteryState? GetBattery()
    {
        if (PowerManager.BatteryStatus == BatteryStatus.NotPresent) return null;
        TimeSpan? remaining = PowerManager.RemainingDischargeTime is { } t && t != TimeSpan.MaxValue && t.TotalDays < 2 ? t : null;
        return new BatteryState(
            PowerManager.RemainingChargePercent,
            PowerManager.BatteryStatus == BatteryStatus.Charging || PowerManager.PowerSupplyStatus != PowerSupplyStatus.NotPresent,
            remaining,
            PowerManager.EnergySaverStatus == EnergySaverStatus.On);
    }

    /// <summary>Публичного API для включения экономии нет: открываем нужную страницу «Параметров».</summary>
    public bool SetBatterySaver(bool on)
    {
        SystemActions.Open("ms-settings:batterysaver");
        return false;
    }

    /// <summary>Вызывать из потока, который живёт всё время работы (UI-поток): флаг привязан к потоку.</summary>
    public void SetKeepAwake(bool on)
    {
        NativeMethods.SetThreadExecutionState(on
            ? NativeMethods.ES_CONTINUOUS | NativeMethods.ES_SYSTEM_REQUIRED | NativeMethods.ES_DISPLAY_REQUIRED
            : NativeMethods.ES_CONTINUOUS);
        IsKeepAwake = on;
    }
}
