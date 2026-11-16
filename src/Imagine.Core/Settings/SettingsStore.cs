using System.Text.Json;
using System.Text.Json.Serialization;

namespace Imagine.Core.Settings;

/// <summary>
/// Загрузка и сохранение настроек в JSON. Запись атомарная (через временный файл),
/// битый файл не теряется — переименовывается в settings.broken-*.json.
/// </summary>
public sealed class SettingsStore
{
    public static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    private readonly string _path;
    private readonly object _lock = new();
    private ImagineSettings _current;

    public SettingsStore(string path)
    {
        _path = path;
        _current = LoadFromDisk();
    }

    public static string DefaultPath =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Imagine", "settings.json");

    public string Path => _path;

    public ImagineSettings Current
    {
        get { lock (_lock) return _current; }
    }

    public event EventHandler<ImagineSettings>? Changed;

    public static JsonSerializerOptions CreateOptions()
    {
        var o = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        o.Converters.Add(new JsonStringEnumConverter());
        return o;
    }

    public void Update(Func<ImagineSettings, ImagineSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        ImagineSettings next;
        lock (_lock)
        {
            next = change(_current);
            _current = next;
            Save(next);
        }
        Changed?.Invoke(this, next);
    }

    public void Reload()
    {
        ImagineSettings next;
        lock (_lock) next = _current = LoadFromDisk();
        Changed?.Invoke(this, next);
    }

    private ImagineSettings LoadFromDisk()
    {
        if (!File.Exists(_path)) return new ImagineSettings();
        try
        {
            var json = File.ReadAllText(_path);
            var s = JsonSerializer.Deserialize<ImagineSettings>(json, JsonOptions) ?? new ImagineSettings();
            return Migrate(s);
        }
        catch (JsonException)
        {
            var broken = System.IO.Path.ChangeExtension(_path, $".broken-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            try { File.Move(_path, broken, overwrite: true); } catch (IOException) { }
            return new ImagineSettings();
        }
    }

    private void Save(ImagineSettings settings)
    {
        var dir = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(tmp, _path, overwrite: true);
    }

    /// <summary>Миграции между версиями формата.</summary>
    public static ImagineSettings Migrate(ImagineSettings s)
    {
        ArgumentNullException.ThrowIfNull(s);
        // Версия 1 — первая; здесь будут шаги вида «if (s.Version < 2) s = …».
        return s.Version == ImagineSettings.CurrentVersion ? s : s with { Version = ImagineSettings.CurrentVersion };
    }
}
