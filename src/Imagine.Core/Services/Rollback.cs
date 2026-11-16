using System.Text.Json;

namespace Imagine.Core.Services;

public enum RegistryValueKind { DWord, String, Binary, ExpandString }

/// <summary>Минимальный доступ к HKCU, достаточный для темы. В тестах подменяется <see cref="InMemoryRegistry"/>.</summary>
public interface IRegistry
{
    object? GetValue(string keyPath, string name);
    void SetValue(string keyPath, string name, object value, RegistryValueKind kind);
    void DeleteValue(string keyPath, string name);
}

public sealed class InMemoryRegistry : IRegistry
{
    private readonly Dictionary<(string, string), (object Value, RegistryValueKind Kind)> _values = [];

    public object? GetValue(string keyPath, string name)
        => _values.TryGetValue((keyPath.ToUpperInvariant(), name.ToUpperInvariant()), out var v) ? v.Value : null;

    public void SetValue(string keyPath, string name, object value, RegistryValueKind kind)
        => _values[(keyPath.ToUpperInvariant(), name.ToUpperInvariant())] = (value, kind);

    public void DeleteValue(string keyPath, string name) => _values.Remove((keyPath.ToUpperInvariant(), name.ToUpperInvariant()));
}

/// <summary>Сохранённое значение: null — значения не было (при откате удаляем).</summary>
public sealed record RegistryEntrySnapshot(string KeyPath, string Name, RegistryValueKind Kind, string? Base64OrText, uint? DWord);

/// <summary>Снимок системных настроек до применения темы.</summary>
public sealed record SystemSnapshot(DateTimeOffset CreatedAt, string Reason, IReadOnlyList<RegistryEntrySnapshot> Registry, string? WallpaperPath);

/// <summary>
/// Записывает значения реестра с запоминанием старых, чтобы откатить всё одной кнопкой.
/// Снимки хранятся в %LOCALAPPDATA%\Imagine\snapshots (последние 20).
/// </summary>
public sealed class RegistryTransaction(IRegistry registry)
{
    private readonly List<RegistryEntrySnapshot> _before = [];
    private readonly HashSet<(string, string)> _seen = [];

    public IReadOnlyList<RegistryEntrySnapshot> Before => _before;

    public void Set(string keyPath, string name, object value, RegistryValueKind kind)
    {
        if (_seen.Add((keyPath.ToUpperInvariant(), name.ToUpperInvariant())))
            _before.Add(Capture(registry, keyPath, name, kind));
        registry.SetValue(keyPath, name, value, kind);
    }

    public static RegistryEntrySnapshot Capture(IRegistry registry, string keyPath, string name, RegistryValueKind kind)
    {
        ArgumentNullException.ThrowIfNull(registry);
        var old = registry.GetValue(keyPath, name);
        return old switch
        {
            null => new RegistryEntrySnapshot(keyPath, name, kind, null, null),
            int i => new RegistryEntrySnapshot(keyPath, name, RegistryValueKind.DWord, null, unchecked((uint)i)),
            uint u => new RegistryEntrySnapshot(keyPath, name, RegistryValueKind.DWord, null, u),
            byte[] b => new RegistryEntrySnapshot(keyPath, name, RegistryValueKind.Binary, Convert.ToBase64String(b), null),
            _ => new RegistryEntrySnapshot(keyPath, name, kind == RegistryValueKind.ExpandString ? kind : RegistryValueKind.String, old.ToString(), null),
        };
    }

    /// <summary>Возвращает значения из снимка (в обратном порядке).</summary>
    public static void Restore(IRegistry registry, IEnumerable<RegistryEntrySnapshot> entries)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(entries);
        foreach (var e in entries.Reverse())
        {
            if (e.DWord is null && e.Base64OrText is null)
            {
                registry.DeleteValue(e.KeyPath, e.Name);
                continue;
            }
            object value = e.Kind switch
            {
                RegistryValueKind.DWord => unchecked((int)e.DWord!.Value),
                RegistryValueKind.Binary => Convert.FromBase64String(e.Base64OrText!),
                _ => e.Base64OrText!,
            };
            registry.SetValue(e.KeyPath, e.Name, value, e.Kind);
        }
    }
}

public sealed class SnapshotStore(string directory, int keep = 20)
{
    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Imagine", "snapshots");

    public string Save(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"snapshot-{snapshot.CreatedAt:yyyyMMdd-HHmmssfff}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(snapshot, Settings.SettingsStore.JsonOptions));
        foreach (var old in List().Skip(keep)) File.Delete(old);
        return path;
    }

    /// <summary>Файлы снимков, новые первыми.</summary>
    public IReadOnlyList<string> List()
        => Directory.Exists(directory)
            ? Directory.GetFiles(directory, "snapshot-*.json").OrderByDescending(f => f, StringComparer.Ordinal).ToList()
            : [];

    public SystemSnapshot? Load(string path)
        => JsonSerializer.Deserialize<SystemSnapshot>(File.ReadAllText(path), Settings.SettingsStore.JsonOptions);

    /// <summary>Самый первый снимок — состояние до первого запуска Imagine.</summary>
    public SystemSnapshot? Oldest() => List().LastOrDefault() is { } p ? Load(p) : null;
}
