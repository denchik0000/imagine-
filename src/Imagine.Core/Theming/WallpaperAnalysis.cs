using Imagine.Color;

namespace Imagine.Core.Theming;

/// <summary>Цвет, найденный на обоях, и его доля среди пикселей.</summary>
public sealed record DominantColor(uint Argb, double Proportion)
{
    public Hct Hct => Hct.FromArgb(Argb);
    public string Hex => ColorUtils.ToHex(Argb);
}

/// <summary>Что утилита узнала об обоях.</summary>
public sealed record WallpaperAnalysis
{
    /// <summary>Кандидаты в затравочный цвет, лучшие первыми (как предлагает Android в «Цвета обоев»).</summary>
    public required IReadOnlyList<uint> SeedCandidates { get; init; }

    /// <summary>Основные цвета снимка по убыванию доли (включая почти серые).</summary>
    public required IReadOnlyList<DominantColor> DominantColors { get; init; }

    /// <summary>Средний тон L* (0 — чёрный, 100 — белый).</summary>
    public required double AverageTone { get; init; }

    /// <summary>Средний тон верхней трети снимка: по нему выбираем цвет часов и шторок.</summary>
    public required double TopAreaTone { get; init; }

    /// <summary>Доля насыщенных пикселей (хрома ≥ 15), 0..1.</summary>
    public required double Colorfulness { get; init; }

    public required int Width { get; init; }
    public required int Height { get; init; }

    public bool IsPortrait => Height > Width;

    /// <summary>Затравка по умолчанию.</summary>
    public uint Seed => SeedCandidates[0];

    /// <summary>Тёмные ли обои: тогда по умолчанию выбираем тёмную тему.</summary>
    public bool PrefersDarkTheme => AverageTone < 50;

    /// <summary>Светлая ли верхняя часть: тогда часы и текст поверх обоев должны быть тёмными.</summary>
    public bool TopIsLight => TopAreaTone >= 60;
}
