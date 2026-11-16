using Imagine.Color;
using Imagine.Core.Integrations;
using Imagine.Core.Settings;
using Imagine.Core.Theming;

namespace Imagine.Core.Services;

/// <summary>Результат применения: тема и что получилось или нет.</summary>
public sealed record ApplyReport(ThemePalette Theme, IReadOnlyList<string> Applied, IReadOnlyList<string> Failed, string? SnapshotPath);

/// <summary>
/// Сердце Imagine: обои → анализ → палитра → акцент Windows, режим, терминал, VS Code, подсветка.
/// Все системные вызовы идут через интерфейсы, поэтому оркестрацию можно проверять тестами.
/// </summary>
public sealed class PersonalizationEngine
{
    private readonly SettingsStore _settings;
    private readonly IImageDecoder _decoder;
    private readonly IRegistry _registry;
    private readonly IAccentService? _accent;
    private readonly ILightingService? _lighting;
    private readonly SnapshotStore? _snapshots;
    private readonly IWallpaperService? _wallpapers;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private (string Path, DateTime Modified, WallpaperAnalysis Analysis)? _analysisCache;

    public PersonalizationEngine(
        SettingsStore settings,
        IImageDecoder decoder,
        IRegistry registry,
        IAccentService? accent = null,
        ILightingService? lighting = null,
        SnapshotStore? snapshots = null,
        IWallpaperService? wallpapers = null)
    {
        _settings = settings;
        _decoder = decoder;
        _registry = registry;
        _accent = accent;
        _lighting = lighting;
        _snapshots = snapshots;
        _wallpapers = wallpapers;
    }

    public ThemePalette? CurrentTheme { get; private set; }
    public WallpaperAnalysis? CurrentAnalysis { get; private set; }

    /// <summary>Затравка обложки текущего трека (ставит сервис медиа).</summary>
    public uint? NowPlayingSeed { get; set; }

    public event EventHandler<ThemePalette>? ThemeChanged;

    public async Task<WallpaperAnalysis?> AnalyzeAsync(string? path, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        var modified = File.GetLastWriteTimeUtc(path);
        if (_analysisCache is { } c && c.Path == path && c.Modified == modified) return c.Analysis;
        var pixels = await _decoder.DecodeAsync(path, WallpaperAnalyzer.AnalysisSide * 2, ct).ConfigureAwait(false);
        var analysis = await Task.Run(() => WallpaperAnalyzer.Analyze(pixels), ct).ConfigureAwait(false);
        _analysisCache = (path, modified, analysis);
        return analysis;
    }

    /// <summary>Только посчитать тему (для превью), ничего не применяя.</summary>
    public async Task<ThemePalette> PreviewAsync(ImagineSettings? settings = null, DateTimeOffset? now = null, CancellationToken ct = default)
    {
        var s = settings ?? _settings.Current;
        var wallpaperPath = s.Wallpaper.Path ?? _wallpapers?.GetWallpaper();
        var analysis = await AnalyzeAsync(wallpaperPath, ct).ConfigureAwait(false);
        CurrentAnalysis = analysis;
        return ThemeEngine.Generate(new ThemeRequest
        {
            Wallpaper = analysis,
            Options = s.Theme,
            Glass = s.Glass,
            Schedule = s.Schedule,
            Now = now ?? DateTimeOffset.Now,
            NowPlayingSeed = s.Apply.NowPlayingTint ? NowPlayingSeed : null,
        });
    }

    /// <summary>Посчитать и применить тему ко всей системе.</summary>
    public async Task<ApplyReport> ApplyAsync(string reason = "Применение темы", DateTimeOffset? now = null, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var s = _settings.Current;
            var theme = await PreviewAsync(s, now, ct).ConfigureAwait(false);
            var applied = new List<string>();
            var failed = new List<string>();
            var tx = new RegistryTransaction(_registry);

            if (s.Apply.WindowsAccent || s.Apply.WindowsThemeMode)
            {
                Try("Акцент и режим Windows", () =>
                {
                    WindowsAccentWriter.Apply(tx, theme.Accent, new WindowsAccentWriter.Options(
                        s.Apply.AccentOnTaskbarAndStart,
                        s.Apply.AccentOnTitleBars,
                        s.Apply.WindowsThemeMode ? theme.IsDark : null,
                        s.Apply.WindowsThemeMode ? (s.Apply.AccentOnTaskbarAndStart || theme.IsDark) : null,
                        s.Apply.Transparency));
                    _accent?.Broadcast();
                }, applied, failed);
            }

            if (s.Apply.WindowsTerminal)
            {
                var path = s.Integrations.WindowsTerminalSettingsPath ?? KnownPaths.WindowsTerminalSettings();
                if (path is not null)
                    Try("Windows Terminal", () => JsonSettingsMerger.WriteWithBackup(path,
                        JsonSettingsMerger.MergeWindowsTerminal(File.Exists(path) ? File.ReadAllText(path) : null, theme, s.Integrations.SetTerminalDefaultScheme)), applied, failed);
                else failed.Add("Windows Terminal: настройки не найдены");
            }

            if (s.Apply.VsCode)
            {
                var path = s.Integrations.VsCodeSettingsPath ?? KnownPaths.VsCodeSettings();
                if (path is not null)
                    Try("VS Code", () => JsonSettingsMerger.WriteWithBackup(path,
                        JsonSettingsMerger.MergeVsCode(File.Exists(path) ? File.ReadAllText(path) : null, theme)), applied, failed);
                else failed.Add("VS Code: настройки не найдены");
            }

            if (s.Apply.DynamicLighting && _lighting is not null)
            {
                try
                {
                    await _lighting.ApplyAsync(theme).ConfigureAwait(false);
                    applied.Add($"Подсветка ({_lighting.DeviceCount} устр.)");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failed.Add("Подсветка: " + ex.Message);
                }
            }

            string? snapshotPath = null;
            if (s.General.SnapshotBeforeApply && _snapshots is not null && tx.Before.Count > 0)
                snapshotPath = _snapshots.Save(new SystemSnapshot(DateTimeOffset.Now, reason, tx.Before, _wallpapers?.GetWallpaper()));

            CurrentTheme = theme;
            ThemeChanged?.Invoke(this, theme);
            return new ApplyReport(theme, applied, failed, snapshotPath);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Откат к снимку (по умолчанию — к самому первому, «как было до Imagine»).</summary>
    public bool Restore(string? snapshotPath = null)
    {
        if (_snapshots is null) return false;
        var snap = snapshotPath is null ? _snapshots.Oldest() : _snapshots.Load(snapshotPath);
        if (snap is null) return false;
        RegistryTransaction.Restore(_registry, snap.Registry);
        if (snap.WallpaperPath is { } wp && File.Exists(wp)) _wallpapers?.SetWallpaper(wp);
        _accent?.Broadcast();
        return true;
    }

    /// <summary>Пользователь выбрал обои: ставим их, при необходимости на экран блокировки, и перекрашиваем систему.</summary>
    public async Task<ApplyReport> SetWallpaperAsync(string path, ILockScreenService? lockScreen = null, CancellationToken ct = default)
    {
        _settings.Update(s => s with { Wallpaper = s.Wallpaper with { Path = path }, Theme = s.Theme with { WallpaperSeedIndex = 0 } });
        var s = _settings.Current;
        _wallpapers?.SetWallpaper(path, null, s.Wallpaper.Fit);
        if (s.Wallpaper.SyncLockScreen && lockScreen is { IsSupported: true })
        {
            try { await lockScreen.SetImageAsync(path, ct).ConfigureAwait(false); }
            catch (Exception ex) when (ex is not OperationCanceledException) { /* экран блокировки не критичен */ }
        }
        return await ApplyAsync("Новые обои", null, ct).ConfigureAwait(false);
    }

    private static void Try(string name, Action action, List<string> applied, List<string> failed)
    {
        try
        {
            action();
            applied.Add(name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
        {
            failed.Add($"{name}: {ex.Message}");
        }
    }
}

/// <summary>Стандартные пути настроек популярных программ.</summary>
public static class KnownPaths
{
    public static string? WindowsTerminalSettings()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[] candidates =
        [
            Path.Combine(local, "Packages", "Microsoft.WindowsTerminal_8wekyb3d8bbwe", "LocalState", "settings.json"),
            Path.Combine(local, "Packages", "Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe", "LocalState", "settings.json"),
            Path.Combine(local, "Microsoft", "Windows Terminal", "settings.json"),
        ];
        return candidates.FirstOrDefault(File.Exists);
    }

    public static string? VsCodeSettings()
    {
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string[] candidates =
        [
            Path.Combine(roaming, "Code", "User", "settings.json"),
            Path.Combine(roaming, "Code - Insiders", "User", "settings.json"),
            Path.Combine(roaming, "VSCodium", "User", "settings.json"),
        ];
        return candidates.FirstOrDefault(File.Exists);
    }
}
