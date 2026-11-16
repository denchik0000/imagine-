using System.Drawing;
using System.Drawing.Imaging;
using Imagine.Core.Theming;

namespace Imagine.Platform.Services;

/// <summary>
/// Подготовка картинки перед установкой: учитываем EXIF-ориентацию (фото с телефона часто «лежат» боком),
/// а если вертикальный кадр после поворота точно совпадает по пропорциям с экраном — поворачиваем его.
/// </summary>
public static class WallpaperPreparer
{
    private const int ExifOrientation = 0x0112;

    /// <summary>Возвращает путь к исправленной копии или исходный путь, если менять ничего не нужно.</summary>
    public static string Prepare(string path, string outputDirectory, int screenWidth, int screenHeight)
    {
        using var image = Image.FromFile(path);
        var flip = RotateFlipType.RotateNoneFlipNone;
        if (Array.IndexOf(image.PropertyIdList, ExifOrientation) >= 0)
        {
            var value = image.GetPropertyItem(ExifOrientation)?.Value;
            int orientation = value is { Length: > 0 } ? value[0] : 1;
            flip = orientation switch
            {
                2 => RotateFlipType.RotateNoneFlipX,
                3 => RotateFlipType.Rotate180FlipNone,
                4 => RotateFlipType.Rotate180FlipX,
                5 => RotateFlipType.Rotate90FlipX,
                6 => RotateFlipType.Rotate90FlipNone,
                7 => RotateFlipType.Rotate270FlipX,
                8 => RotateFlipType.Rotate270FlipNone,
                _ => RotateFlipType.RotateNoneFlipNone,
            };
        }
        bool swapped = flip is RotateFlipType.Rotate90FlipNone or RotateFlipType.Rotate90FlipX or RotateFlipType.Rotate270FlipNone or RotateFlipType.Rotate270FlipX;
        int w = swapped ? image.Height : image.Width, h = swapped ? image.Width : image.Height;
        if (WallpaperAnalyzer.SuggestsRotation(w, h, screenWidth, screenHeight))
        {
            flip = flip switch
            {
                RotateFlipType.RotateNoneFlipNone => RotateFlipType.Rotate90FlipNone,
                RotateFlipType.Rotate90FlipNone => RotateFlipType.Rotate180FlipNone,
                RotateFlipType.Rotate180FlipNone => RotateFlipType.Rotate270FlipNone,
                RotateFlipType.Rotate270FlipNone => RotateFlipType.RotateNoneFlipNone,
                _ => flip,
            };
        }
        if (flip == RotateFlipType.RotateNoneFlipNone) return path;

        Directory.CreateDirectory(outputDirectory);
        image.RotateFlip(flip);
        if (Array.IndexOf(image.PropertyIdList, ExifOrientation) >= 0) image.RemovePropertyItem(ExifOrientation);
        var output = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(path) + "-imagine.jpg");
        var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(Encoder.Quality, 95L);
        image.Save(output, codec, parameters);
        return output;
    }
}
