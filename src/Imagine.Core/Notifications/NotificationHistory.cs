namespace Imagine.Core.Notifications;

/// <summary>История смахнутых уведомлений: можно вернуться и перечитать (в Windows такой нет).</summary>
public sealed class NotificationHistory
{
    private readonly LinkedList<(NotificationItem Item, DateTimeOffset DismissedAt)> _items = new();
    private readonly object _lock = new();

    public NotificationHistory(int capacity = 200, TimeSpan? retention = null)
    {
        Capacity = Math.Max(1, capacity);
        Retention = retention ?? TimeSpan.FromDays(1);
    }

    public int Capacity { get; }
    public TimeSpan Retention { get; }

    public void Add(NotificationItem item, DateTimeOffset dismissedAt)
    {
        lock (_lock)
        {
            _items.AddFirst((item, dismissedAt));
            while (_items.Count > Capacity) _items.RemoveLast();
        }
    }

    public IReadOnlyList<NotificationItem> Snapshot(DateTimeOffset now)
    {
        lock (_lock)
        {
            while (_items.Last is { } last && now - last.Value.DismissedAt > Retention) _items.RemoveLast();
            return _items.Select(x => x.Item).ToList();
        }
    }

    public void Clear()
    {
        lock (_lock) _items.Clear();
    }
}
