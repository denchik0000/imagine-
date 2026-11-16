using Imagine.Core.Services;
using Imagine.Core.Theming;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Imagine.Platform.Services;

/// <summary>Чтение картинок через WIC (Windows.Graphics.Imaging): JPEG, PNG, HEIC, WebP, AVIF — всё, для чего стоят кодеки.</summary>
public sealed class WinRtImageDecoder : IImageDecoder
{
    public async Task<PixelBuffer> DecodeAsync(string path, int maxSide, CancellationToken ct = default)
    {
        var file = await StorageFile.GetFileFromPathAsync(path).AsTask(ct).ConfigureAwait(false);
        using var stream = await file.OpenReadAsync().AsTask(ct).ConfigureAwait(false);
        return await DecodeStreamAsync(stream, maxSide, ct).ConfigureAwait(false);
    }

    public async Task<PixelBuffer> DecodeBytesAsync(byte[] data, int maxSide, CancellationToken ct = default)
    {
        using var ms = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(ms.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(data);
            await writer.StoreAsync().AsTask(ct).ConfigureAwait(false);
            await writer.FlushAsync().AsTask(ct).ConfigureAwait(false);
            writer.DetachStream();
        }
        ms.Seek(0);
        return await DecodeStreamAsync(ms, maxSide, ct).ConfigureAwait(false);
    }

    public async Task<(int Width, int Height)> GetSizeAsync(string path, CancellationToken ct = default)
    {
        var file = await StorageFile.GetFileFromPathAsync(path).AsTask(ct).ConfigureAwait(false);
        using var stream = await file.OpenReadAsync().AsTask(ct).ConfigureAwait(false);
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(ct).ConfigureAwait(false);
        return ((int)decoder.OrientedPixelWidth, (int)decoder.OrientedPixelHeight);
    }

    private static async Task<PixelBuffer> DecodeStreamAsync(IRandomAccessStream stream, int maxSide, CancellationToken ct)
    {
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(ct).ConfigureAwait(false);
        uint w = decoder.OrientedPixelWidth, h = decoder.OrientedPixelHeight;
        double scale = Math.Min(1.0, (double)maxSide / Math.Max(w, h));
        var transform = new BitmapTransform
        {
            ScaledWidth = Math.Max(1, (uint)Math.Round(decoder.PixelWidth * scale)),
            ScaledHeight = Math.Max(1, (uint)Math.Round(decoder.PixelHeight * scale)),
            InterpolationMode = BitmapInterpolationMode.Fant,
        };
        var data = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, transform,
            ExifOrientationMode.RespectExifOrientation, ColorManagementMode.ColorManageToSRgb).AsTask(ct).ConfigureAwait(false);
        var bytes = data.DetachPixelData();
        // После учёта EXIF ширина и высота могли поменяться местами.
        int outW = (int)Math.Max(1, Math.Round(w * scale)), outH = (int)Math.Max(1, Math.Round(h * scale));
        if (outW * outH * 4 != bytes.Length) (outW, outH) = ((int)transform.ScaledWidth, (int)transform.ScaledHeight);
        if (outW * outH * 4 != bytes.Length) (outW, outH) = (outH, outW);
        return PixelBuffer.FromBgra(outW, outH, bytes);
    }
}
