using System.Globalization;
using System.Text.Json;

namespace Imagine.Color.Tests;

/// <summary>
/// Эталонные значения, посчитанные Python-портом material-color-utilities (materialyoucolor 3.0.4, спецификация 2021).
/// Скрипт генерации — tools/golden/gen_golden.py.
/// </summary>
internal static class Golden
{
    private static readonly Lazy<JsonDocument> Doc = new(() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "golden.json"))));

    public static JsonElement Root => Doc.Value.RootElement;

    public static uint[] LoadWallpaper()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "wallpaper-72x128.argb.txt");
        return File.ReadLines(path)
            .Where(l => !l.StartsWith('#'))
            .SelectMany(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Select(h => uint.Parse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture))
            .ToArray();
    }

    public static string Hex(uint argb) => ColorUtils.ToHex(argb);
}
