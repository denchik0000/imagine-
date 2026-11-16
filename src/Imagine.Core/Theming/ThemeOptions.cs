using Imagine.Color;

namespace Imagine.Core.Theming;

/// <summary>Как выбирать светлую или тёмную тему.</summary>
public enum ThemeModePreference
{
    /// <summary>По яркости обоев.</summary>
    FromWallpaper,
    /// <summary>По времени суток (закат/рассвет или заданные часы).</summary>
    BySchedule,
    Light,
    Dark,
}

/// <summary>Откуда брать затравочный цвет.</summary>
public enum SeedSource
{
    /// <summary>Лучший кандидат с обоев (или выбранный по номеру).</summary>
    Wallpaper,
    /// <summary>Цвет, заданный пользователем.</summary>
    Custom,
    /// <summary>Обложка играющего трека (временно, пока играет музыка).</summary>
    NowPlaying,
}

/// <summary>Параметры генерации темы.</summary>
public sealed record ThemeOptions
{
    public Variant Variant { get; init; } = Variant.TonalSpot;
    public ThemeModePreference Mode { get; init; } = ThemeModePreference.FromWallpaper;
    public SeedSource SeedSource { get; init; } = SeedSource.Wallpaper;
    /// <summary>Номер кандидата с обоев (0 — лучший).</summary>
    public int WallpaperSeedIndex { get; init; }
    public uint? CustomSeed { get; init; }
    /// <summary>−1 … 1.</summary>
    public double ContrastLevel { get; init; }
    /// <summary>Тёплый сдвиг оттенка вечером, градусы (0 — выключено).</summary>
    public double EveningWarmShift { get; init; } = 8;
    /// <summary>Насколько сильно обложка трека перекрашивает палитру (0..1).</summary>
    public double NowPlayingBlend { get; init; } = 0.65;
}
