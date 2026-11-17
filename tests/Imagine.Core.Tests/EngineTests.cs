using Imagine.Core.Services;
using Imagine.Core.Settings;
using Imagine.Core.Theming;

namespace Imagine.Core.Tests;

public class EngineTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "imagine-engine-" + Guid.NewGuid().ToString("N"));

    public EngineTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        Directory.Delete(_dir, true);
        GC.SuppressFinalize(this);
    }

    private sealed class FakeDecoder(PixelBuffer image) : IImageDecoder
    {
        public int Calls { get; private set; }
        public Task<PixelBuffer> DecodeAsync(string path, int maxSide, CancellationToken ct = default) { Calls++; return Task.FromResult(image); }
        public Task<(int Width, int Height)> GetSizeAsync(string path, CancellationToken ct = default) => Task.FromResult((image.Width, image.Height));
    }

    private sealed class FakeAccent : IAccentService
    {
        public int Broadcasts;
        public WindowsAccentPalette? ReadCurrent() => null;
        public void Apply(WindowsAccentPalette palette, bool showOnTaskbarAndStart, bool showOnTitleBars) { }
        public void SetThemeMode(bool appsDark, bool systemDark) { }
        public void SetTransparency(bool enabled) { }
        public void Broadcast() => Broadcasts++;
    }

    private sealed class FakeWallpaper : IWallpaperService
    {
        public string? Current = @"C:\old.jpg";
        public IReadOnlyList<MonitorInfo> GetMonitors() => [];
        public string? GetWallpaper(string? monitorDevicePath = null) => Current;
        public void SetWallpaper(string imagePath, string? monitorDevicePath = null, WallpaperFit fit = WallpaperFit.Fill) => Current = imagePath;
        public void SetSlideshow(string folder, TimeSpan interval, bool shuffle) { }
    }

    [Fact]
    public async Task Apply_writes_accent_snapshots_and_restores()
    {
        var wallpaper = Path.Combine(_dir, "wall.png");
        File.WriteAllBytes(wallpaper, [1, 2, 3]);
        var store = new SettingsStore(Path.Combine(_dir, "settings.json"));
        store.Update(s => s with { Wallpaper = s.Wallpaper with { Path = wallpaper } });
        var reg = new InMemoryRegistry();
        reg.SetValue(WindowsAccentWriter.PersonalizeKey, "AppsUseLightTheme", 1, RegistryValueKind.DWord);
        var accent = new FakeAccent();
        var decoder = new FakeDecoder(ThemingTests.Stripes(64, 64, 0xFF1A1020, 0xFFD55E5D));
        var snapshots = new SnapshotStore(Path.Combine(_dir, "snap"));
        var engine = new PersonalizationEngine(store, decoder, reg, accent, null, snapshots, new FakeWallpaper());

        ThemePalette? changed = null;
        engine.ThemeChanged += (_, t) => changed = t;
        var report = await engine.ApplyAsync();

        Assert.NotNull(changed);
        Assert.Contains("Акцент и режим Windows", report.Applied);
        Assert.Empty(report.Failed);
        Assert.NotNull(report.SnapshotPath);
        Assert.Equal(1, accent.Broadcasts);
        Assert.NotNull(reg.GetValue(WindowsAccentWriter.AccentKey, "AccentPalette"));
        Assert.Equal(report.Theme.IsDark ? 0 : 1, reg.GetValue(WindowsAccentWriter.PersonalizeKey, "AppsUseLightTheme"));

        // Повторный анализ того же файла берётся из кэша.
        await engine.PreviewAsync();
        Assert.Equal(1, decoder.Calls);

        Assert.True(engine.Restore());
        Assert.Equal(1, reg.GetValue(WindowsAccentWriter.PersonalizeKey, "AppsUseLightTheme"));
        Assert.Null(reg.GetValue(WindowsAccentWriter.AccentKey, "AccentPalette"));
    }

    [Fact]
    public async Task Set_wallpaper_updates_settings_and_system()
    {
        var wallpaper = Path.Combine(_dir, "new.png");
        File.WriteAllBytes(wallpaper, [1]);
        var store = new SettingsStore(Path.Combine(_dir, "s.json"));
        var wp = new FakeWallpaper();
        var engine = new PersonalizationEngine(store, new FakeDecoder(ThemingTests.Stripes(16, 16, 0xFF2E7D32)), new InMemoryRegistry(), wallpapers: wp);
        var report = await engine.SetWallpaperAsync(wallpaper);
        Assert.Equal(wallpaper, wp.Current);
        Assert.Equal(wallpaper, store.Current.Wallpaper.Path);
        Assert.NotNull(engine.CurrentAnalysis);
        Assert.InRange(Imagine.Color.Hct.FromArgb(report.Theme.Seed).Hue, 100, 160); // зелёный
    }

    [Fact]
    public async Task Missing_terminal_is_reported_not_thrown()
    {
        var store = new SettingsStore(Path.Combine(_dir, "s2.json"));
        store.Update(s => s with
        {
            Apply = s.Apply with { WindowsTerminal = true, WindowsAccent = false, WindowsThemeMode = false },
            Integrations = s.Integrations with { WindowsTerminalSettingsPath = Path.Combine(_dir, "nope", "settings.json") },
        });
        var engine = new PersonalizationEngine(store, new FakeDecoder(ThemingTests.Stripes(8, 8, 0xFF3366CC)), new InMemoryRegistry());
        var report = await engine.ApplyAsync();
        Assert.Contains(report.Failed, f => f.StartsWith("Windows Terminal", StringComparison.Ordinal));
    }
}
