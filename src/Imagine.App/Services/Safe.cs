using Microsoft.UI.Dispatching;

namespace Imagine.App;

/// <summary>
/// Безопасная постановка в очередь интерфейса. Исключение внутри обычного TryEnqueue не попадает
/// в Application.UnhandledException и закрывает приложение (сбой 0xc000027b в CoreMessaging) —
/// поэтому всё, что уходит в очередь, идёт через эти методы и пишет ошибку в журнал.
/// </summary>
public static class Safe
{
    public static bool Post(this DispatcherQueue queue, Action action, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
        => queue.TryEnqueue(priority, () => Run(action));

    public static bool Post(this DispatcherQueue queue, Func<Task> action)
        => queue.TryEnqueue(async () =>
        {
            try { await action(); }
            catch (Exception ex) { Log.Error("Ошибка в очереди интерфейса", ex); }
        });

    public static void Run(Action action, string what = "Ошибка в очереди интерфейса")
    {
        try { action(); }
        catch (Exception ex) { Log.Error(what, ex); }
    }
}
