namespace Imagine.Core.Shades;

/// <summary>Плитки шторки быстрых настроек.</summary>
public enum QuickTileId
{
    Wifi,
    Bluetooth,
    Airplane,
    DarkMode,
    NightLight,
    Focus,
    BatterySaver,
    Hotspot,
    MicMute,
    Mute,
    KeepAwake,
    Screenshot,
    ColorPicker,
    Calculator,
    Projection,
    Settings,
    /// <summary>Следующий стиль палитры (Tonal Spot → Vibrant → Expressive …).</summary>
    PaletteStyle,
    /// <summary>Перемешать: следующий кандидат цвета с обоев.</summary>
    PaletteShuffle,
    /// <summary>Следующие обои из папки.</summary>
    NextWallpaper,
    ClipboardHistory,
    EmojiPanel,
    LockScreen,
}

/// <summary>Размер плитки: маленькая круглая (как в ColorOS) или широкая «пилюля» с подписью и подменю.</summary>
public enum QuickTileSize { Compact, Wide }

public sealed record QuickTileSlot(QuickTileId Id, QuickTileSize Size = QuickTileSize.Compact);

/// <summary>Расположение плиток: порядок, размеры, число колонок. Широкая плитка занимает две колонки.</summary>
public sealed class QuickTileLayout
{
    private readonly List<QuickTileSlot> _slots;

    public QuickTileLayout(IEnumerable<QuickTileSlot> slots, int columns = 4)
    {
        _slots = slots.DistinctBy(s => s.Id).ToList();
        Columns = Math.Clamp(columns, 2, 8);
    }

    public static QuickTileLayout Default => new(
    [
        new(QuickTileId.Wifi, QuickTileSize.Wide),
        new(QuickTileId.Bluetooth, QuickTileSize.Wide),
        new(QuickTileId.Focus),
        new(QuickTileId.DarkMode),
        new(QuickTileId.NightLight),
        new(QuickTileId.Airplane),
        new(QuickTileId.BatterySaver),
        new(QuickTileId.MicMute),
        new(QuickTileId.KeepAwake),
        new(QuickTileId.Screenshot),
        new(QuickTileId.PaletteStyle),
        new(QuickTileId.PaletteShuffle),
    ]);

    public int Columns { get; }

    public IReadOnlyList<QuickTileSlot> Slots => _slots;

    public IEnumerable<QuickTileId> Available => Enum.GetValues<QuickTileId>().Except(_slots.Select(s => s.Id));

    public QuickTileLayout Move(QuickTileId id, int newIndex)
    {
        var list = _slots.ToList();
        int i = list.FindIndex(s => s.Id == id);
        if (i < 0) return this;
        var slot = list[i];
        list.RemoveAt(i);
        list.Insert(Math.Clamp(newIndex, 0, list.Count), slot);
        return new QuickTileLayout(list, Columns);
    }

    public QuickTileLayout Add(QuickTileId id, QuickTileSize size = QuickTileSize.Compact, int? index = null)
    {
        if (_slots.Any(s => s.Id == id)) return this;
        var list = _slots.ToList();
        list.Insert(Math.Clamp(index ?? list.Count, 0, list.Count), new QuickTileSlot(id, size));
        return new QuickTileLayout(list, Columns);
    }

    public QuickTileLayout Remove(QuickTileId id) => new(_slots.Where(s => s.Id != id), Columns);

    public QuickTileLayout Resize(QuickTileId id, QuickTileSize size)
        => new(_slots.Select(s => s.Id == id ? s with { Size = size } : s), Columns);

    /// <summary>Раскладывает плитки по сетке: (плитка, строка, колонка, ширина в колонках). Широкая плитка не разрывается между строками.</summary>
    public IReadOnlyList<(QuickTileSlot Slot, int Row, int Column, int Span)> Arrange()
    {
        var result = new List<(QuickTileSlot, int, int, int)>();
        var occupied = new HashSet<(int, int)>();
        foreach (var slot in _slots)
        {
            int span = slot.Size == QuickTileSize.Wide ? Math.Min(2, Columns) : 1;
            for (int cell = 0; ; cell++)
            {
                int row = cell / Columns, col = cell % Columns;
                if (col + span > Columns) continue;
                bool free = true;
                for (int k = 0; k < span; k++) if (occupied.Contains((row, col + k))) { free = false; break; }
                if (!free) continue;
                for (int k = 0; k < span; k++) occupied.Add((row, col + k));
                result.Add((slot, row, col, span));
                break;
            }
        }
        return result;
    }

    public int RowCount => Arrange().Select(a => a.Row).DefaultIfEmpty(-1).Max() + 1;
}
