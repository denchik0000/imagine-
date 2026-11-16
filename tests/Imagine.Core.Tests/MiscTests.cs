using System.Text.Json.Nodes;
using Imagine.Color;
using Imagine.Core.Clock;
using Imagine.Core.Hotkeys;
using Imagine.Core.Integrations;
using Imagine.Core.Services;
using Imagine.Core.Settings;
using Imagine.Core.Theming;

namespace Imagine.Core.Tests;

public class HotkeyTests
{
    [Theory]
    [InlineData("Win+Alt+N", HotkeyModifiers.Win | HotkeyModifiers.Alt, 0x4E)]
    [InlineData("ctrl + shift + F12", HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x7B)]
    [InlineData("Win+Space", HotkeyModifiers.Win, 0x20)]
    [InlineData("Alt+`", HotkeyModifiers.Alt, 0xC0)]
    public void Parses_gestures(string text, HotkeyModifiers mods, int vk)
    {
        var g = HotkeyGesture.Parse(text);
        Assert.Equal(mods, g.Modifiers);
        Assert.Equal(vk, g.VirtualKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Win+Alt")]
    [InlineData("Ctrl+A+B")]
    [InlineData("Ctrl+F99")]
    public void Rejects_invalid(string text) => Assert.False(HotkeyGesture.TryParse(text, out _));

    [Fact]
    public void Roundtrips_to_canonical_text()
        => Assert.Equal("Win+Ctrl+Alt+Shift+K", HotkeyGesture.Parse("shift+alt+ctrl+win+k").ToString());
}

public class ClockTests
{
    [Fact]
    public void Formats_russian_dates()
    {
        var d = new DateTime(2026, 11, 16, 9, 5, 7);
        Assert.Equal("Понедельник, 16 ноября", ClockFormatter.LongDate(d));
        var (h, m, s, ampm) = ClockFormatter.Digits(d, new ClockStyle { ShowSeconds = true });
        Assert.Equal(("09", "05", "07", null), (h, m, s, ampm));
        var twelve = ClockFormatter.Digits(new DateTime(2026, 1, 1, 0, 30, 0), new ClockStyle { Use24Hour = false });
        Assert.Equal("12", twelve.Hours);
        Assert.Equal("AM", twelve.AmPm);
    }

    [Fact]
    public void Hand_angles()
    {
        var (h, m, s) = ClockFormatter.HandAngles(new DateTime(2026, 1, 1, 3, 0, 0));
        Assert.Equal(90, h, 6);
        Assert.Equal(0, m, 6);
        Assert.Equal(0, s, 6);
    }

    [Fact]
    public void Clock_shrinks_with_notifications_but_not_below_limit()
    {
        var style = new ClockStyle();
        var none = FluidClockLayout.Compute(style, 1920, 1080);
        var three = FluidClockLayout.Compute(style, 1920, 1080, 3);
        var many = FluidClockLayout.Compute(style, 1920, 1080, 50);
        Assert.True(three.FontSize < none.FontSize);
        Assert.True(many.FontSize >= none.FontSize * 0.55 - 1e-9);
    }

    [Fact]
    public void Clock_placements_stay_on_screen()
    {
        foreach (var p in Enum.GetValues<ClockPlacement>())
        {
            var f = FluidClockLayout.Compute(new ClockStyle { Placement = p }, 1920, 1080);
            Assert.InRange(f.X, 0, 1920 - f.Width);
            Assert.InRange(f.Y, 0, 1080 - f.Height);
        }
    }

    [Fact]
    public void Interpolation_is_linear()
    {
        var a = new ClockFrame(0, 0, 10, 100, 50);
        var b = new ClockFrame(100, 200, 30, 300, 150);
        Assert.Equal(new ClockFrame(50, 100, 20, 200, 100), FluidClockLayout.Interpolate(a, b, 0.5));
    }
}

public class SettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "imagine-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Settings_roundtrip_through_disk()
    {
        var path = Path.Combine(_dir, "settings.json");
        var store = new SettingsStore(path);
        store.Update(s => s with
        {
            Theme = s.Theme with { Variant = Variant.Expressive, CustomSeed = 0xFFD55E5D },
            Shades = s.Shades with { NotificationsHotkey = "Win+Shift+N" },
        });
        var reloaded = new SettingsStore(path).Current;
        Assert.Equal(Variant.Expressive, reloaded.Theme.Variant);
        Assert.Equal(0xFFD55E5Du, reloaded.Theme.CustomSeed);
        Assert.Equal("Win+Shift+N", reloaded.Shades.NotificationsHotkey);
        Assert.Contains("\"Expressive\"", File.ReadAllText(path));
    }

    [Fact]
    public void Broken_settings_file_is_kept_aside()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, "{ это не json");
        var store = new SettingsStore(path);
        Assert.Equal(ImagineSettings.CurrentVersion, store.Current.Version);
        Assert.Single(Directory.GetFiles(_dir, "settings.broken-*.json"));
    }

    [Fact]
    public void Settings_tolerate_comments()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, "{ // комментарий\n \"theme\": { \"variant\": \"Vibrant\", }, }");
        Assert.Equal(Variant.Vibrant, new SettingsStore(path).Current.Theme.Variant);
    }

    [Fact]
    public void Changed_event_fires()
    {
        var store = new SettingsStore(Path.Combine(_dir, "s.json"));
        int calls = 0;
        store.Changed += (_, _) => calls++;
        store.Update(s => s with { Glass = s.Glass with { Frost = 0.1 } });
        Assert.Equal(1, calls);
    }
}

public class RollbackTests
{
    [Fact]
    public void Transaction_restores_previous_values_and_deletes_new_ones()
    {
        var reg = new InMemoryRegistry();
        reg.SetValue(WindowsAccentWriter.DwmKey, "ColorPrevalence", 0, RegistryValueKind.DWord);
        reg.SetValue(WindowsAccentWriter.AccentKey, "AccentPalette", new byte[32], RegistryValueKind.Binary);

        var tx = new RegistryTransaction(reg);
        var scheme = new DynamicScheme(0xFFD55E5D, Variant.TonalSpot, true);
        WindowsAccentWriter.Apply(tx, WindowsAccentPalette.FromScheme(scheme), new WindowsAccentWriter.Options(true, true, true, true, true));

        Assert.Equal(1, reg.GetValue(WindowsAccentWriter.DwmKey, "ColorPrevalence"));
        Assert.Equal(0, reg.GetValue(WindowsAccentWriter.PersonalizeKey, "AppsUseLightTheme"));
        Assert.NotEqual(new byte[32], (byte[])reg.GetValue(WindowsAccentWriter.AccentKey, "AccentPalette")!);

        RegistryTransaction.Restore(reg, tx.Before);
        Assert.Equal(0, reg.GetValue(WindowsAccentWriter.DwmKey, "ColorPrevalence"));
        Assert.Equal(new byte[32], (byte[])reg.GetValue(WindowsAccentWriter.AccentKey, "AccentPalette")!);
        Assert.Null(reg.GetValue(WindowsAccentWriter.PersonalizeKey, "AppsUseLightTheme"));
    }

    [Fact]
    public void Same_value_written_twice_keeps_original_snapshot()
    {
        var reg = new InMemoryRegistry();
        reg.SetValue("K", "V", 5, RegistryValueKind.DWord);
        var tx = new RegistryTransaction(reg);
        tx.Set("K", "V", 6, RegistryValueKind.DWord);
        tx.Set("K", "V", 7, RegistryValueKind.DWord);
        Assert.Single(tx.Before);
        RegistryTransaction.Restore(reg, tx.Before);
        Assert.Equal(5, reg.GetValue("K", "V"));
    }

    [Fact]
    public void Snapshot_store_keeps_newest()
    {
        var dir = Path.Combine(Path.GetTempPath(), "imagine-snap-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SnapshotStore(dir, keep: 3);
            for (int i = 0; i < 5; i++)
                store.Save(new SystemSnapshot(new DateTimeOffset(2026, 1, 1, 0, 0, i, TimeSpan.Zero), $"#{i}", [], null));
            Assert.Equal(3, store.List().Count);
            Assert.Equal("#4", store.Load(store.List()[0])!.Reason);
            Assert.Equal("#2", store.Oldest()!.Reason);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}

public class IntegrationTests
{
    private static ThemePalette Theme() => ThemeEngine.Generate(new ThemeRequest
    {
        Options = new ThemeOptions { SeedSource = SeedSource.Custom, CustomSeed = 0xFFD55E5D, Mode = ThemeModePreference.Dark },
    });

    [Fact]
    public void Terminal_merge_preserves_user_content_and_is_idempotent()
    {
        const string existing = """
        {
            // мой терминал
            "defaultProfile": "{abc}",
            "profiles": { "defaults": { "font": { "face": "Cascadia Code" } }, "list": [ { "name": "PowerShell" } ] },
            "schemes": [ { "name": "Campbell", "background": "#0C0C0C" } ],
        }
        """;
        var once = JsonSettingsMerger.MergeWindowsTerminal(existing, Theme(), setAsDefault: true);
        var twice = JsonSettingsMerger.MergeWindowsTerminal(once, Theme(), setAsDefault: true);
        Assert.Equal(once, twice);
        var root = JsonNode.Parse(twice)!.AsObject();
        Assert.Equal("{abc}", root["defaultProfile"]!.GetValue<string>());
        Assert.Equal(2, root["schemes"]!.AsArray().Count);
        Assert.Equal("Imagine", root["profiles"]!["defaults"]!["colorScheme"]!.GetValue<string>());
        Assert.Equal("Cascadia Code", root["profiles"]!["defaults"]!["font"]!["face"]!.GetValue<string>());
        Assert.Equal("Imagine", root["theme"]!.GetValue<string>());
    }

    [Fact]
    public void Terminal_merge_handles_legacy_profiles_array_and_empty_file()
    {
        var merged = JsonSettingsMerger.MergeWindowsTerminal("""{ "profiles": [ { "name": "cmd" } ] }""", Theme(), true);
        var root = JsonNode.Parse(merged)!.AsObject();
        Assert.Equal("cmd", root["profiles"]!["list"]![0]!["name"]!.GetValue<string>());
        Assert.NotNull(JsonNode.Parse(JsonSettingsMerger.MergeWindowsTerminal(null, Theme(), false))!["schemes"]);
    }

    [Fact]
    public void VsCode_merge_and_removal_keep_foreign_keys()
    {
        const string existing = """{ "editor.fontSize": 14, "workbench.colorCustomizations": { "my.key": "#123456" } }""";
        var merged = JsonSettingsMerger.MergeVsCode(existing, Theme());
        var root = JsonNode.Parse(merged)!.AsObject();
        Assert.Equal(14, root["editor.fontSize"]!.GetValue<int>());
        Assert.Equal("#123456", root["workbench.colorCustomizations"]!["my.key"]!.GetValue<string>());
        Assert.NotNull(root["workbench.colorCustomizations"]!["editor.background"]);

        var removed = JsonNode.Parse(JsonSettingsMerger.RemoveFromVsCode(merged, Theme()))!.AsObject();
        Assert.Single(removed["workbench.colorCustomizations"]!.AsObject());
    }

    [Fact]
    public void Write_with_backup_keeps_first_original()
    {
        var dir = Path.Combine(Path.GetTempPath(), "imagine-int-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "settings.json");
            File.WriteAllText(path, "original");
            JsonSettingsMerger.WriteWithBackup(path, "v1");
            JsonSettingsMerger.WriteWithBackup(path, "v2");
            Assert.Equal("v2", File.ReadAllText(path));
            Assert.Equal("original", File.ReadAllText(path + ".imagine-backup"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
