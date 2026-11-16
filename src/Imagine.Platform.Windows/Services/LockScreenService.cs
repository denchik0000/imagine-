using Imagine.Core.Services;
using Imagine.Platform.Interop;
using Windows.Storage;
using Windows.System.UserProfile;

namespace Imagine.Platform.Services;

/// <summary>Картинка экрана блокировки. Windows разрешает это только упакованным приложениям (MSIX).</summary>
public sealed class LockScreenService : ILockScreenService
{
    public bool IsSupported { get; } = NativeMethods.HasPackageIdentity();

    public async Task SetImageAsync(string imagePath, CancellationToken ct = default)
    {
        if (!IsSupported) throw new PlatformNotSupportedException("Экран блокировки доступен только в версии MSIX.");
        var file = await StorageFile.GetFileFromPathAsync(imagePath).AsTask(ct).ConfigureAwait(false);
        await LockScreen.SetImageFileAsync(file).AsTask(ct).ConfigureAwait(false);
    }
}
