using System.Runtime.InteropServices;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Imagine.App.Ui;

/// <summary>
/// «Матовое стекло» ColorOS: акрил рабочего стола с тинтом из палитры обоев.
/// Тинт и плотность можно менять на лету — шторки перекрашиваются вместе с темой.
/// </summary>
public sealed class GlassBackdrop : SystemBackdrop
{
    private readonly List<DesktopAcrylicController> _controllers = [];
    private Windows.UI.Color _tint = Palette.ToColor(0xFF2A1F1F);
    private float _tintOpacity = 0.55f;
    private float _luminosity = 0.6f;

    public void SetTint(uint argb, double tintOpacity, double luminosityOpacity)
    {
        _tint = Palette.ToColor(argb | 0xFF000000);
        _tintOpacity = (float)Math.Clamp(tintOpacity, 0, 1);
        _luminosity = (float)Math.Clamp(luminosityOpacity, 0, 1);
        foreach (var c in _controllers) Configure(c);
    }

    private void Configure(DesktopAcrylicController c)
    {
        c.TintColor = _tint;
        c.TintOpacity = _tintOpacity;
        c.LuminosityOpacity = _luminosity;
        c.FallbackColor = Windows.UI.Color.FromArgb(0xF0, _tint.R, _tint.G, _tint.B);
    }

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);
        var controller = new DesktopAcrylicController { Kind = DesktopAcrylicKind.Base };
        Configure(controller);
        var config = GetDefaultSystemBackdropConfiguration(connectedTarget, xamlRoot);
        config.IsInputActive = true; // стекло не тускнеет, когда окно без фокуса
        controller.SetSystemBackdropConfiguration(config);
        controller.AddSystemBackdropTarget(connectedTarget);
        _controllers.Add(controller);
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        base.OnTargetDisconnected(disconnectedTarget);
        foreach (var c in _controllers.ToList())
        {
            if (c.RemoveSystemBackdropTarget(disconnectedTarget))
            {
                c.Dispose();
                _controllers.Remove(c);
            }
        }
    }
}

/// <summary>
/// Полностью прозрачный фон окна (для часов на рабочем столе и островка).
/// Использует системный компоновщик Windows.UI.Composition; если он недоступен, окно остаётся с акрилом.
/// </summary>
public sealed class TransparentBackdrop : SystemBackdrop
{
    private static Windows.UI.Composition.Compositor? _compositor;
    private static object? _queueController;

    public bool Failed { get; private set; }

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);
        try
        {
            EnsureSystemDispatcherQueue();
            _compositor ??= new Windows.UI.Composition.Compositor();
            connectedTarget.SystemBackdrop = _compositor.CreateColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or TypeInitializationException)
        {
            Failed = true;
        }
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        base.OnTargetDisconnected(disconnectedTarget);
        disconnectedTarget.SystemBackdrop = null;
    }

    private static void EnsureSystemDispatcherQueue()
    {
        if (Windows.System.DispatcherQueue.GetForCurrentThread() is not null || _queueController is not null) return;
        var options = new DispatcherQueueOptions { dwSize = Marshal.SizeOf<DispatcherQueueOptions>(), threadType = 2, apartmentType = 2 };
        Marshal.ThrowExceptionForHR(CreateDispatcherQueueController(options, out var ptr));
        _queueController = WinRT.MarshalInspectable<Windows.System.DispatcherQueueController>.FromAbi(ptr);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DispatcherQueueOptions
    {
        public int dwSize;
        public int threadType;
        public int apartmentType;
    }

    [DllImport("CoreMessaging.dll")]
    private static extern int CreateDispatcherQueueController(DispatcherQueueOptions options, out IntPtr controller);
}
