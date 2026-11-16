using Imagine.Color;

namespace Imagine.Core.Theming;

/// <summary>
/// Восемь оттенков акцента, которые Windows хранит в HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Accent\AccentPalette
/// (32 байта: R, G, B, A для каждого). Порядок: Light3, Light2, Light1, Accent, Dark1, Dark2, Dark3, запасной.
/// </summary>
public sealed record WindowsAccentPalette(uint Light3, uint Light2, uint Light1, uint Accent, uint Dark1, uint Dark2, uint Dark3, uint Extra)
{
    /// <summary>Тоны HCT, близкие к тому, как Windows сама строит палитру акцента.</summary>
    private static readonly double[] Tones = [88, 77, 66, 52, 42, 32, 22, 12];

    /// <summary>
    /// Строит палитру из основного цвета схемы. Хрома берётся из основной палитры, но не меньше 24,
    /// чтобы панель задач и Пуск не выглядели серыми.
    /// </summary>
    public static WindowsAccentPalette FromScheme(DynamicScheme scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        var p = scheme.PrimaryPalette;
        double chroma = Math.Max(p.Chroma, scheme.Variant == Variant.Monochrome ? 0 : 24);
        var palette = Math.Abs(chroma - p.Chroma) < 0.01 ? p : TonalPalette.FromHueAndChroma(p.Hue, chroma);
        uint T(int i) => palette.Tone(Tones[i]);
        return new WindowsAccentPalette(T(0), T(1), T(2), T(3), T(4), T(5), T(6), T(7));
    }

    public IReadOnlyList<uint> All => [Light3, Light2, Light1, Accent, Dark1, Dark2, Dark3, Extra];

    /// <summary>Значение REG_BINARY для AccentPalette.</summary>
    public byte[] ToRegistryBlob()
    {
        var bytes = new byte[32];
        int i = 0;
        foreach (var c in All)
        {
            bytes[i++] = (byte)ColorUtils.RedFromArgb(c);
            bytes[i++] = (byte)ColorUtils.GreenFromArgb(c);
            bytes[i++] = (byte)ColorUtils.BlueFromArgb(c);
            bytes[i++] = 0;
        }
        return bytes;
    }

    public static WindowsAccentPalette FromRegistryBlob(ReadOnlySpan<byte> blob)
    {
        if (blob.Length < 32) throw new ArgumentException("AccentPalette должен быть 32 байта.", nameof(blob));
        var c = new uint[8];
        for (int i = 0; i < 8; i++) c[i] = ColorUtils.ArgbFromRgb(blob[i * 4], blob[i * 4 + 1], blob[i * 4 + 2]);
        return new WindowsAccentPalette(c[0], c[1], c[2], c[3], c[4], c[5], c[6], c[7]);
    }

    /// <summary>DWORD в формате ABGR (0xFFBBGGRR), как его ждут AccentColorMenu, StartColorMenu и DWM\AccentColor.</summary>
    public static uint ToAbgr(uint argb)
        => 0xFF000000u | ((uint)ColorUtils.BlueFromArgb(argb) << 16) | ((uint)ColorUtils.GreenFromArgb(argb) << 8) | (uint)ColorUtils.RedFromArgb(argb);

    public static uint FromAbgr(uint abgr)
        => ColorUtils.ArgbFromRgb((int)(abgr & 255), (int)((abgr >> 8) & 255), (int)((abgr >> 16) & 255));

    /// <summary>AccentColorMenu: основной акцент.</summary>
    public uint AccentColorMenu => ToAbgr(Accent);

    /// <summary>StartColorMenu: фон Пуска при «показывать цвет на Пуске», Windows берёт Dark2.</summary>
    public uint StartColorMenu => ToAbgr(Dark2);

    /// <summary>DWM\ColorizationColor: ARGB с прозрачностью 0xC4.</summary>
    public uint ColorizationColor => 0xC4000000u | (Accent & 0xFFFFFF);
}
