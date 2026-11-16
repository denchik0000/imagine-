using System.Text.Json;
using System.Text.Json.Nodes;
using Imagine.Core.Exporters;
using Imagine.Core.Theming;

namespace Imagine.Core.Integrations;

/// <summary>
/// Вписывает тему в настройки чужих программ, не трогая остальное содержимое.
/// Файлы с комментариями (JSONC) читаются, но комментарии при записи теряются,
/// поэтому перед записью всегда делается резервная копия.
/// </summary>
public static class JsonSettingsMerger
{
    private static readonly JsonDocumentOptions ReadOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public const string SchemeName = "Imagine";

    /// <summary>settings.json Windows Terminal: добавляет/обновляет схему и тему «Imagine», опционально делает схему схемой по умолчанию.</summary>
    public static string MergeWindowsTerminal(string? existingJson, ThemePalette theme, bool setAsDefault)
    {
        var root = Parse(existingJson);
        var schemes = root["schemes"] as JsonArray ?? new JsonArray();
        root["schemes"] = schemes;
        ReplaceByName(schemes, ThemeExporters.WindowsTerminalScheme(theme, SchemeName));

        var themes = root["themes"] as JsonArray ?? new JsonArray();
        root["themes"] = themes;
        ReplaceByName(themes, ThemeExporters.WindowsTerminalTheme(theme, SchemeName));

        if (setAsDefault)
        {
            var profiles = root["profiles"] as JsonObject;
            if (profiles is null)
            {
                // Формат, где profiles — массив: переносим его в list.
                var list = root["profiles"] as JsonArray;
                profiles = new JsonObject();
                if (list is not null)
                {
                    root.Remove("profiles");
                    profiles["list"] = list;
                }
                root["profiles"] = profiles;
            }
            var defaults = profiles["defaults"] as JsonObject ?? new JsonObject();
            profiles["defaults"] = defaults;
            defaults["colorScheme"] = SchemeName;
            root["theme"] = SchemeName;
        }
        return root.ToJsonString(WriteOptions);
    }

    /// <summary>settings.json VS Code: пишет workbench.colorCustomizations (только наши ключи, чужие оставляет).</summary>
    public static string MergeVsCode(string? existingJson, ThemePalette theme)
    {
        var root = Parse(existingJson);
        var custom = root["workbench.colorCustomizations"] as JsonObject ?? new JsonObject();
        root["workbench.colorCustomizations"] = custom;
        foreach (var (key, value) in ThemeExporters.VsCodeColorCustomizations(theme))
            custom[key] = value?.DeepClone();
        return root.ToJsonString(WriteOptions);
    }

    /// <summary>Убирает наши ключи из VS Code (для отката).</summary>
    public static string RemoveFromVsCode(string existingJson, ThemePalette anyTheme)
    {
        var root = Parse(existingJson);
        if (root["workbench.colorCustomizations"] is JsonObject custom)
        {
            foreach (var (key, _) in ThemeExporters.VsCodeColorCustomizations(anyTheme)) custom.Remove(key);
            if (custom.Count == 0) root.Remove("workbench.colorCustomizations");
        }
        return root.ToJsonString(WriteOptions);
    }

    /// <summary>Записывает файл с резервной копией (*.imagine-backup) рядом, если копии ещё нет.</summary>
    public static void WriteWithBackup(string path, string content)
    {
        if (File.Exists(path))
        {
            var backup = path + ".imagine-backup";
            if (!File.Exists(backup)) File.Copy(path, backup);
        }
        var tmp = path + ".imagine-tmp";
        File.WriteAllText(tmp, content);
        File.Move(tmp, path, overwrite: true);
    }

    private static JsonObject Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new JsonObject();
        return JsonNode.Parse(json, documentOptions: ReadOptions) as JsonObject
               ?? throw new JsonException("Ожидался JSON-объект настроек.");
    }

    private static void ReplaceByName(JsonArray array, JsonObject item)
    {
        string name = item["name"]!.GetValue<string>();
        for (int i = 0; i < array.Count; i++)
        {
            if (array[i] is JsonObject o && o["name"]?.GetValue<string>() == name)
            {
                array[i] = item;
                return;
            }
        }
        array.Add(item);
    }
}
