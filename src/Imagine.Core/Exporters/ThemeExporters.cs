using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Imagine.Color;
using Imagine.Core.Theming;

namespace Imagine.Core.Exporters;

/// <summary>Экспорт темы во внешние форматы.</summary>
public static class ThemeExporters
{
    private static string H(uint c) => ColorUtils.ToHex(c);

    private static string Camel(ColorRole role)
    {
        var s = role.ToString();
        return char.ToLowerInvariant(s[0]) + s[1..];
    }

    private static string Kebab(ColorRole role)
    {
        var sb = new StringBuilder();
        foreach (var ch in role.ToString())
        {
            if (char.IsUpper(ch) && sb.Length > 0) sb.Append('-');
            sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }

    /// <summary>Цветовая схема Windows Terminal (объект для массива "schemes").</summary>
    public static JsonObject WindowsTerminalScheme(ThemePalette theme, string name = "Imagine")
    {
        var a = AnsiPalette.From(theme);
        return new JsonObject
        {
            ["name"] = name,
            ["background"] = H(a.Background),
            ["foreground"] = H(a.Foreground),
            ["cursorColor"] = H(a.Cursor),
            ["selectionBackground"] = H(a.Selection),
            ["black"] = H(a.Black),
            ["red"] = H(a.Red),
            ["green"] = H(a.Green),
            ["yellow"] = H(a.Yellow),
            ["blue"] = H(a.Blue),
            ["purple"] = H(a.Purple),
            ["cyan"] = H(a.Cyan),
            ["white"] = H(a.White),
            ["brightBlack"] = H(a.BrightBlack),
            ["brightRed"] = H(a.BrightRed),
            ["brightGreen"] = H(a.BrightGreen),
            ["brightYellow"] = H(a.BrightYellow),
            ["brightBlue"] = H(a.BrightBlue),
            ["brightPurple"] = H(a.BrightPurple),
            ["brightCyan"] = H(a.BrightCyan),
            ["brightWhite"] = H(a.BrightWhite),
        };
    }

    /// <summary>Тема Windows Terminal (вкладки и заголовок окна в цвет палитры).</summary>
    public static JsonObject WindowsTerminalTheme(ThemePalette theme, string name = "Imagine") => new()
    {
        ["name"] = name,
        ["window"] = new JsonObject
        {
            ["applicationTheme"] = theme.IsDark ? "dark" : "light",
            ["useMica"] = true,
        },
        ["tabRow"] = new JsonObject
        {
            ["background"] = H(theme[ColorRole.SurfaceContainer]) + "CC",
            ["unfocusedBackground"] = H(theme[ColorRole.SurfaceContainerLow]) + "CC",
        },
        ["tab"] = new JsonObject
        {
            ["background"] = H(theme[ColorRole.PrimaryContainer]) + "FF",
            ["unfocusedBackground"] = H(theme[ColorRole.SurfaceContainerHigh]) + "FF",
            ["showCloseButton"] = "hover",
        },
    };

    /// <summary>"workbench.colorCustomizations" для VS Code.</summary>
    public static JsonObject VsCodeColorCustomizations(ThemePalette theme)
    {
        string C(ColorRole r) => H(theme[r]);
        string CA(ColorRole r, int alpha) => H(theme[r]) + alpha.ToString("X2", CultureInfo.InvariantCulture);
        var a = AnsiPalette.From(theme);
        return new JsonObject
        {
            ["focusBorder"] = C(ColorRole.Primary),
            ["foreground"] = C(ColorRole.OnSurface),
            ["selection.background"] = CA(ColorRole.PrimaryContainer, 0x99),
            ["editor.background"] = C(ColorRole.Surface),
            ["editor.foreground"] = C(ColorRole.OnSurface),
            ["editor.lineHighlightBackground"] = CA(ColorRole.SurfaceContainerHigh, 0x80),
            ["editor.selectionBackground"] = CA(ColorRole.PrimaryContainer, 0xAA),
            ["editorCursor.foreground"] = C(ColorRole.Primary),
            ["editorLineNumber.foreground"] = C(ColorRole.Outline),
            ["editorLineNumber.activeForeground"] = C(ColorRole.Primary),
            ["editorGroupHeader.tabsBackground"] = C(ColorRole.SurfaceContainerLow),
            ["tab.activeBackground"] = C(ColorRole.Surface),
            ["tab.inactiveBackground"] = C(ColorRole.SurfaceContainerLow),
            ["tab.activeBorderTop"] = C(ColorRole.Primary),
            ["activityBar.background"] = C(ColorRole.SurfaceContainer),
            ["activityBar.foreground"] = C(ColorRole.Primary),
            ["activityBarBadge.background"] = C(ColorRole.Primary),
            ["activityBarBadge.foreground"] = C(ColorRole.OnPrimary),
            ["sideBar.background"] = C(ColorRole.SurfaceContainerLow),
            ["sideBarSectionHeader.background"] = C(ColorRole.SurfaceContainer),
            ["statusBar.background"] = C(ColorRole.PrimaryContainer),
            ["statusBar.foreground"] = C(ColorRole.OnPrimaryContainer),
            ["titleBar.activeBackground"] = C(ColorRole.SurfaceContainer),
            ["titleBar.activeForeground"] = C(ColorRole.OnSurface),
            ["button.background"] = C(ColorRole.Primary),
            ["button.foreground"] = C(ColorRole.OnPrimary),
            ["badge.background"] = C(ColorRole.Tertiary),
            ["badge.foreground"] = C(ColorRole.OnTertiary),
            ["list.activeSelectionBackground"] = C(ColorRole.SecondaryContainer),
            ["list.activeSelectionForeground"] = C(ColorRole.OnSecondaryContainer),
            ["list.hoverBackground"] = CA(ColorRole.SurfaceContainerHighest, 0x99),
            ["input.background"] = C(ColorRole.SurfaceContainerHighest),
            ["panel.background"] = C(ColorRole.SurfaceContainerLow),
            ["terminal.background"] = H(a.Background),
            ["terminal.foreground"] = H(a.Foreground),
            ["terminal.ansiRed"] = H(a.Red),
            ["terminal.ansiGreen"] = H(a.Green),
            ["terminal.ansiYellow"] = H(a.Yellow),
            ["terminal.ansiBlue"] = H(a.Blue),
            ["terminal.ansiMagenta"] = H(a.Purple),
            ["terminal.ansiCyan"] = H(a.Cyan),
        };
    }

    /// <summary>CSS-переменные в стиле Material 3 (--md-sys-color-*), например для userstyles браузера.</summary>
    public static string Css(ThemePalette theme, string selector = ":root")
    {
        var sb = new StringBuilder();
        sb.Append("/* Imagine: тема из обоев, затравка ").Append(H(theme.Seed)).Append(", ").Append(theme.Variant).AppendLine(" */");
        void Block(string sel, DynamicScheme s)
        {
            sb.Append(sel).AppendLine(" {");
            foreach (var role in Enum.GetValues<ColorRole>())
                sb.Append("  --md-sys-color-").Append(Kebab(role)).Append(": ").Append(H(s.Get(role))).AppendLine(";");
            sb.AppendLine("}");
        }
        Block(selector, theme.Scheme);
        sb.AppendLine();
        sb.AppendLine("@media (prefers-color-scheme: " + (theme.IsDark ? "light" : "dark") + ") {");
        Block("  " + selector, theme.OppositeScheme);
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>JSON со всеми ролями обеих схем и палитрами — для своих скриптов и плагинов.</summary>
    public static string Json(ThemePalette theme)
    {
        JsonObject Scheme(DynamicScheme s)
        {
            var o = new JsonObject();
            foreach (var role in Enum.GetValues<ColorRole>()) o[Camel(role)] = H(s.Get(role));
            return o;
        }
        JsonObject Palette(TonalPalette p)
        {
            var o = new JsonObject();
            foreach (var t in new[] { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 95, 99, 100 }) o[t.ToString(CultureInfo.InvariantCulture)] = H(p.Tone(t));
            return o;
        }
        var s = theme.Scheme;
        var root = new JsonObject
        {
            ["seed"] = H(theme.Seed),
            ["variant"] = theme.Variant.ToString(),
            ["contrastLevel"] = theme.ContrastLevel,
            ["schemes"] = new JsonObject { ["light"] = Scheme(theme.LightScheme), ["dark"] = Scheme(theme.DarkScheme) },
            ["palettes"] = new JsonObject
            {
                ["primary"] = Palette(s.PrimaryPalette),
                ["secondary"] = Palette(s.SecondaryPalette),
                ["tertiary"] = Palette(s.TertiaryPalette),
                ["neutral"] = Palette(s.NeutralPalette),
                ["neutralVariant"] = Palette(s.NeutralVariantPalette),
                ["error"] = Palette(s.ErrorPalette),
            },
            ["windowsAccent"] = new JsonArray(theme.Accent.All.Select(c => (JsonNode)H(c)).ToArray()),
        };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Файл темы Windows (.theme): запасной путь применения акцента и режима, если запись в реестр не сработала.
    /// Двойной щелчок по файлу применяет тему штатными средствами Windows.
    /// </summary>
    public static string WindowsThemeFile(ThemePalette theme, string? wallpaperPath, string displayName = "Imagine")
    {
        var sb = new StringBuilder();
        string mode = theme.IsDark ? "Dark" : "Light";
        sb.AppendLine("; Сгенерировано Imagine");
        sb.AppendLine("[Theme]");
        sb.Append("DisplayName=").AppendLine(displayName);
        sb.AppendLine();
        sb.AppendLine(@"[Control Panel\Desktop]");
        if (!string.IsNullOrEmpty(wallpaperPath)) sb.Append("Wallpaper=").AppendLine(wallpaperPath);
        sb.AppendLine("WallpaperStyle=10");
        sb.AppendLine("TileWallpaper=0");
        sb.AppendLine();
        sb.AppendLine("[VisualStyles]");
        sb.AppendLine(@"Path=%SystemRoot%\resources\Themes\Aero\Aero.msstyles");
        sb.AppendLine("ColorStyle=NormalColor");
        sb.AppendLine("Size=NormalSize");
        sb.Append("ColorizationColor=0X").AppendLine(theme.Accent.ColorizationColor.ToString("X8", CultureInfo.InvariantCulture));
        sb.AppendLine("Transparency=1");
        sb.AppendLine("AutoColorization=0");
        sb.Append("SystemMode=").AppendLine(mode);
        sb.Append("AppMode=").AppendLine(mode);
        sb.AppendLine();
        sb.AppendLine("[MasterThemeSelector]");
        sb.AppendLine("MTSM=RJSPBS");
        return sb.ToString();
    }
}
