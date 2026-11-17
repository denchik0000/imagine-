using Imagine.Core.Shades;

namespace Imagine.App.Shell;

/// <summary>Описание плитки: значок, подпись, переключатель или действие.</summary>
public sealed record QuickTileInfo(QuickTileId Id, string Glyph, string Label, bool IsToggle);

public static class QuickTileCatalog
{
    public static QuickTileInfo Get(QuickTileId id) => id switch
    {
        QuickTileId.Wifi => new(id, Ui.Glyphs.Wifi, "Wi‑Fi", true),
        QuickTileId.Bluetooth => new(id, Ui.Glyphs.Bluetooth, "Bluetooth", true),
        QuickTileId.Airplane => new(id, Ui.Glyphs.Airplane, "В самолёте", true),
        QuickTileId.DarkMode => new(id, Ui.Glyphs.DarkMode, "Тёмная тема", true),
        QuickTileId.NightLight => new(id, Ui.Glyphs.NightLight, "Ночной свет", false),
        QuickTileId.Focus => new(id, Ui.Glyphs.Focus, "Фокус", true),
        QuickTileId.BatterySaver => new(id, Ui.Glyphs.BatterySaver, "Экономия", true),
        QuickTileId.Hotspot => new(id, Ui.Glyphs.Hotspot, "Точка доступа", false),
        QuickTileId.MicMute => new(id, Ui.Glyphs.MicOff, "Без микрофона", true),
        QuickTileId.Mute => new(id, Ui.Glyphs.Mute, "Без звука", true),
        QuickTileId.KeepAwake => new(id, Ui.Glyphs.Coffee, "Не засыпать", true),
        QuickTileId.Screenshot => new(id, Ui.Glyphs.Screenshot, "Снимок", false),
        QuickTileId.ColorPicker => new(id, Ui.Glyphs.ColorPicker, "Цвет с экрана", false),
        QuickTileId.Calculator => new(id, Ui.Glyphs.Calculator, "Калькулятор", false),
        QuickTileId.Projection => new(id, Ui.Glyphs.Project, "Проецировать", false),
        QuickTileId.Settings => new(id, Ui.Glyphs.Settings, "Imagine", false),
        QuickTileId.PaletteStyle => new(id, Ui.Glyphs.Palette, "Стиль палитры", false),
        QuickTileId.PaletteShuffle => new(id, Ui.Glyphs.Shuffle, "Другой цвет", false),
        QuickTileId.NextWallpaper => new(id, Ui.Glyphs.Picture, "Следующие обои", false),
        QuickTileId.ClipboardHistory => new(id, Ui.Glyphs.Clipboard, "Буфер обмена", false),
        QuickTileId.EmojiPanel => new(id, Ui.Glyphs.Emoji, "Эмодзи", false),
        QuickTileId.LockScreen => new(id, Ui.Glyphs.Lock, "Заблокировать", false),
        _ => new(id, Ui.Glyphs.Settings, id.ToString(), false),
    };

    public static string VariantName(Imagine.Color.Variant v) => v switch
    {
        Imagine.Color.Variant.Monochrome => "Монохром",
        Imagine.Color.Variant.Neutral => "Нейтральный",
        Imagine.Color.Variant.TonalSpot => "Пастельный",
        Imagine.Color.Variant.Vibrant => "Яркий",
        Imagine.Color.Variant.Expressive => "Выразительный",
        Imagine.Color.Variant.Fidelity => "Точный",
        Imagine.Color.Variant.Content => "Контентный",
        Imagine.Color.Variant.Rainbow => "Радуга",
        Imagine.Color.Variant.FruitSalad => "Фруктовый салат",
        _ => v.ToString(),
    };
}
