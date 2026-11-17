using Imagine.App.Ui;
using Imagine.Core.Exporters;
using Imagine.Core.Integrations;
using Imagine.Core.Services;
using Imagine.Core.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static Imagine.App.Pages.PageHelpers;

namespace Imagine.App.Pages;

/// <summary>Терминал, VS Code, экспорт палитры в файлы и откат системных изменений.</summary>
internal static class IntegrationsPage
{
    public static UIElement Build(AppServices services, MainWindow window)
    {
        var s = services.Current;
        void Up(Func<IntegrationSettings, IntegrationSettings> f) => services.UpdateSettings(x => x with { Integrations = f(x.Integrations) });
        void Apply(Func<ApplySettings, ApplySettings> f) => services.ThemeSettingsChanged(x => x with { Apply = f(x.Apply) });
        var result = Ui.Ui.Caption("");

        string exportDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Imagine");
        void Export(string file, Func<Core.Theming.ThemePalette, string> make)
        {
            if (Palette.Current is not { } theme) return;
            try
            {
                Directory.CreateDirectory(exportDir);
                var path = Path.Combine(exportDir, file);
                File.WriteAllText(path, make(theme));
                result.Text = "Сохранено: " + path;
            }
            catch (IOException ex) { result.Text = "Не удалось сохранить: " + ex.Message; }
            catch (UnauthorizedAccessException ex) { result.Text = "Не удалось сохранить: " + ex.Message; }
        }
        var json = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };

        var exports = Ui.Ui.VStack(8,
            Ui.Ui.HStack(8,
                Ui.Ui.Button("CSS-переменные", () => Export("imagine.css", t => ThemeExporters.Css(t)), Glyphs.Code),
                Ui.Ui.Button("JSON со всеми ролями", () => Export("imagine.json", ThemeExporters.Json), Glyphs.Code),
                Ui.Ui.Button("Схема Windows Terminal", () => Export("imagine-terminal.json", t => ThemeExporters.WindowsTerminalScheme(t).ToJsonString(json)), Glyphs.Terminal)),
            Ui.Ui.HStack(8,
                Ui.Ui.Button("Цвета VS Code", () => Export("imagine-vscode.json", t => ThemeExporters.VsCodeColorCustomizations(t).ToJsonString(json)), Glyphs.Code),
                Ui.Ui.Button("Тема Windows (.theme)", () => Export("Imagine.theme", t => ThemeExporters.WindowsThemeFile(t, services.Current.Wallpaper.Path)), Glyphs.Palette),
                Ui.Ui.Button("Открыть папку", () => { Directory.CreateDirectory(exportDir); Platform.Services.SystemActions.Open(exportDir); }, Glyphs.Folder)),
            result);

        var snapshots = new StackPanel { Spacing = 6 };
        void RenderSnapshots()
        {
            snapshots.Children.Clear();
            var list = services.Snapshots.List();
            if (list.Count == 0)
            {
                snapshots.Children.Add(Ui.Ui.Caption("Снимков пока нет — они появятся при первом применении темы."));
                return;
            }
            foreach (var path in list.Take(10))
            {
                var snap = services.Snapshots.Load(path);
                if (snap is null) continue;
                var p = path;
                var restore = Ui.Ui.Button("Вернуть", () =>
                {
                    result.Text = services.Engine.Restore(p) ? "Восстановлено состояние от " + snap.CreatedAt.ToLocalTime().ToString("d MMMM HH:mm", System.Globalization.CultureInfo.GetCultureInfo("ru-RU")) : "Не удалось восстановить";
                }, Glyphs.Undo);
                snapshots.Children.Add(Ui.Ui.Grid("*,Auto",
                    Ui.Ui.VStack(0, Ui.Ui.Text(snap.Reason), Ui.Ui.Caption($"{snap.CreatedAt.ToLocalTime():dd.MM.yyyy HH:mm} · {snap.Registry.Count} параметров реестра")),
                    restore));
            }
        }
        RenderSnapshots();

        var wtFound = KnownPaths.WindowsTerminalSettings();
        var codeFound = KnownPaths.VsCodeSettings();

        return Page("Интеграции и откат", "Палитра выходит за пределы Windows: терминал, редактор, сайты. И всё можно вернуть как было.",
            SettingsCard.Group("Программы",
                Toggle(Glyphs.Terminal, "Windows Terminal", wtFound is null ? "Настройки терминала не найдены" : "Добавляет схему и тему «Imagine» в " + wtFound, s.Apply.WindowsTerminal, v => Apply(x => x with { WindowsTerminal = v })),
                Toggle(Glyphs.Terminal, "Сделать схемой по умолчанию", "Для всех профилей терминала", s.Integrations.SetTerminalDefaultScheme, v => Up(x => x with { SetTerminalDefaultScheme = v })),
                Card(Glyphs.Folder, "Свой путь к settings.json терминала", null, Ui.Ui.TextBox(s.Integrations.WindowsTerminalSettingsPath ?? "", v => Up(x => x with { WindowsTerminalSettingsPath = string.IsNullOrWhiteSpace(v) ? null : v.Trim('"') }), wtFound ?? "Путь к файлу", 320)),
                Toggle(Glyphs.Code, "Visual Studio Code", codeFound is null ? "Настройки VS Code не найдены" : "Цвета рабочего места в " + codeFound, s.Apply.VsCode, v =>
                {
                    Apply(x => x with { VsCode = v });
                    if (!v && Palette.Current is not null && (services.Current.Integrations.VsCodeSettingsPath ?? codeFound) is { } path && File.Exists(path))
                        JsonSettingsMerger.WriteWithBackup(path, JsonSettingsMerger.RemoveFromVsCode(File.ReadAllText(path), Palette.Current ?? services.Engine.CurrentTheme!));
                }),
                Card(Glyphs.Folder, "Свой путь к settings.json VS Code", null, Ui.Ui.TextBox(s.Integrations.VsCodeSettingsPath ?? "", v => Up(x => x with { VsCodeSettingsPath = string.IsNullOrWhiteSpace(v) ? null : v.Trim('"') }), codeFound ?? "Путь к файлу", 320))),
            SettingsCard.Group("Экспорт палитры", Card(Glyphs.Export, "Сохранить в файл", "В папку «Документы\\Imagine»", null, exports)),
            SettingsCard.Group("Откат",
                Card(Glyphs.Undo, "Как было до Imagine", "Вернуть акцент, режим, прозрачность и обои к самому первому снимку", Ui.Ui.Button("Вернуть всё", () =>
                {
                    result.Text = services.Engine.Restore() ? "Система возвращена к состоянию до Imagine" : "Снимков нет — Imagine ещё ничего не менял";
                }, Glyphs.Undo)),
                Toggle(Glyphs.Save, "Снимок перед каждым применением", "Хранятся последние 20", s.General.SnapshotBeforeApply, v => services.UpdateSettings(x => x with { General = x.General with { SnapshotBeforeApply = v } })),
                Card(Glyphs.History, "Снимки", null, null, snapshots)));
    }
}
