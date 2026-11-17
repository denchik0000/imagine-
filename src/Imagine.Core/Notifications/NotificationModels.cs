namespace Imagine.Core.Notifications;

/// <summary>Уведомление, прочитанное из центра уведомлений Windows.</summary>
public sealed record NotificationItem
{
    public required uint Id { get; init; }
    public required string AppId { get; init; }
    public required string AppName { get; init; }
    public string Title { get; init; } = "";
    public string Body { get; init; } = "";
    public required DateTimeOffset Timestamp { get; init; }
    /// <summary>Путь к иконке приложения (кэш), если удалось получить.</summary>
    public string? AppIconPath { get; init; }
    /// <summary>Картинка-герой из уведомления, если есть.</summary>
    public string? ImagePath { get; init; }
    public NotificationPriority Priority { get; init; } = NotificationPriority.Normal;
}

public enum NotificationPriority { Low, Normal, High }

/// <summary>Вид уведомлений в шторке (как на экране блокировки ColorOS 17).</summary>
public enum NotificationStyle
{
    /// <summary>Колода: карточки одного приложения лежат стопкой, сверху самая новая.</summary>
    Stack,
    /// <summary>Капсулы: компактные «пилюли» по приложениям, раскрываются по нажатию.</summary>
    Capsule,
    /// <summary>Обычный список.</summary>
    List,
}

/// <summary>Группа уведомлений одного приложения.</summary>
public sealed record NotificationGroup(string AppId, string AppName, string? AppIconPath, IReadOnlyList<NotificationItem> Items)
{
    public NotificationItem Latest => Items[0];
    public int Count => Items.Count;
    public DateTimeOffset LatestTime => Latest.Timestamp;
    public bool HasHighPriority => Items.Any(i => i.Priority == NotificationPriority.High);
}

/// <summary>Карточка для отрисовки: позиция в стопке и масштаб.</summary>
public sealed record NotificationCardView(NotificationItem Item, int DepthInStack, double Scale, double OffsetY, double Opacity, bool IsExpanded);

/// <summary>Что рисовать в шторке.</summary>
public sealed record NotificationGroupView(NotificationGroup Group, IReadOnlyList<NotificationCardView> Cards, bool Collapsed, string Summary);
