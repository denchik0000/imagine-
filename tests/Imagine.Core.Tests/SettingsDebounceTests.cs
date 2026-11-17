using Imagine.Core.Settings;
using Xunit;

namespace Imagine.Core.Tests;

public sealed class SettingsDebounceTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("imagine-debounce").FullName;

    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public void DelayedSaveWritesOnlyOnFlush()
    {
        var path = Path.Combine(_dir, "s.json");
        var store = new SettingsStore(path) { SaveDelay = TimeSpan.FromHours(1) };
        for (int i = 0; i < 50; i++)
            store.Update(s => s with { Glass = s.Glass with { Frost = i / 100.0 } });
        Assert.False(File.Exists(path));
        store.Flush();
        Assert.Equal(0.49, new SettingsStore(path).Current.Glass.Frost, 6);
    }

    [Fact]
    public void UnchangedUpdateRaisesNoEvent()
    {
        var store = new SettingsStore(Path.Combine(_dir, "s2.json"));
        int raised = 0;
        store.Changed += (_, _) => raised++;
        store.Update(s => s);
        Assert.Equal(0, raised);
    }
}
