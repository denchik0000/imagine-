using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using Imagine.Color;
using Imagine.Core.Clock;

namespace Imagine.Platform.Services;

/// <summary>
/// «Вживляет» часы и дату в картинку экрана блокировки: сам экран блокировки Windows менять нельзя,
/// но картинку — можно. Обновляется по таймеру раз в минуту.
/// </summary>
public static class LockScreenComposer
{
    public static string Compose(string wallpaperPath, string outputDirectory, ClockStyle style, uint textColor, DateTime now, int screenWidth, int screenHeight)
    {
        ArgumentNullException.ThrowIfNull(style);
        Directory.CreateDirectory(outputDirectory);
        using var source = Image.FromFile(wallpaperPath);
        using var canvas = new Bitmap(screenWidth, screenHeight, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(canvas))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            DrawCover(g, source, screenWidth, screenHeight);

            var frame = FluidClockLayout.Compute(style, screenWidth, screenHeight);
            var (h, m, _, _) = ClockFormatter.Digits(now, style);
            var color = System.Drawing.Color.FromArgb(ColorUtils.AlphaFromArgb(textColor) == 0 ? 255 : ColorUtils.AlphaFromArgb(textColor),
                ColorUtils.RedFromArgb(textColor), ColorUtils.GreenFromArgb(textColor), ColorUtils.BlueFromArgb(textColor));
            var weight = style.FontWeight >= 600 ? FontStyle.Bold : FontStyle.Regular;
            using var font = new Font(style.FontFamily, (float)frame.FontSize, weight, GraphicsUnit.Pixel);
            using var dateFont = new Font("Segoe UI Variable Text", (float)(frame.FontSize * 0.16), FontStyle.Regular, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(color);
            using var shadow = new SolidBrush(System.Drawing.Color.FromArgb(70, 0, 0, 0));
            float x = (float)frame.X, y = (float)frame.Y;
            void Text(string s, Font f, float tx, float ty)
            {
                if (style.Glow) g.DrawString(s, f, shadow, tx + 2, ty + 3);
                g.DrawString(s, f, brush, tx, ty);
            }
            if (style.Layout == ClockLayout.Stacked)
            {
                Text(h, font, x, y);
                Text(m, font, x, y + (float)(frame.FontSize * 0.86));
                if (style.ShowDate) Text(ClockFormatter.LongDate(now), dateFont, x + 6, y + (float)(frame.FontSize * 1.86));
            }
            else
            {
                Text($"{h}:{m}", font, x, y);
                if (style.ShowDate) Text(ClockFormatter.LongDate(now), dateFont, x + 6, y + (float)(frame.FontSize * 1.02));
            }
        }
        // Windows кэширует картинку по имени файла, поэтому имя каждый раз новое.
        foreach (var old in Directory.GetFiles(outputDirectory, "lock-*.png"))
        {
            try { File.Delete(old); } catch (IOException) { }
        }
        var path = Path.Combine(outputDirectory, $"lock-{now:yyyyMMddHHmm}.png");
        canvas.Save(path, ImageFormat.Png);
        return path;
    }

    private static void DrawCover(Graphics g, Image img, int w, int h)
    {
        double scale = Math.Max((double)w / img.Width, (double)h / img.Height);
        int dw = (int)Math.Ceiling(img.Width * scale), dh = (int)Math.Ceiling(img.Height * scale);
        g.DrawImage(img, (w - dw) / 2, (h - dh) / 2, dw, dh);
    }
}
