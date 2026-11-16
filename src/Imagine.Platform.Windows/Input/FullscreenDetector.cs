using Imagine.Platform.Interop;

namespace Imagine.Platform.Input;

/// <summary>Идёт ли сейчас полноэкранная игра, презентация или видео (тогда шторки с края не открываются).</summary>
public static class FullscreenDetector
{
    public static bool IsFullscreenActive()
    {
        if (NativeMethods.SHQueryUserNotificationState(out int state) != 0) return false;
        return state is NativeMethods.QUNS_BUSY or NativeMethods.QUNS_RUNNING_D3D_FULL_SCREEN or NativeMethods.QUNS_PRESENTATION_MODE;
    }
}
