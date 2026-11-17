using System.Globalization;

namespace Imagine.Core.Notifications;

/// <summary>Раскладывает уведомления по группам и считает вид карточек для Stack / Capsule / List.</summary>
public static class NotificationArranger
{
    /// <summary>Сколько карточек видно в стопке под верхней.</summary>
    public const int VisibleStackDepth = 2;

    public static IReadOnlyList<NotificationGroup> Group(IEnumerable<NotificationItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items
            .GroupBy(i => i.AppId)
            .Select(g =>
            {
                var sorted = g.OrderByDescending(i => i.Timestamp).ThenByDescending(i => i.Id).ToList();
                return new NotificationGroup(g.Key, sorted[0].AppName, sorted.FirstOrDefault(i => i.AppIconPath is not null)?.AppIconPath, sorted);
            })
            .OrderByDescending(g => g.HasHighPriority)
            .ThenByDescending(g => g.LatestTime)
            .ToList();
    }

    public static IReadOnlyList<NotificationGroupView> Arrange(
        IEnumerable<NotificationItem> items,
        NotificationStyle style,
        IReadOnlySet<string>? expandedApps = null,
        DateTimeOffset? now = null)
    {
        var groups = Group(items);
        var current = now ?? DateTimeOffset.Now;
        expandedApps ??= new HashSet<string>();
        var result = new List<NotificationGroupView>(groups.Count);
        foreach (var g in groups)
        {
            bool expanded = expandedApps.Contains(g.AppId);
            string summary = Summary(g, current);
            switch (style)
            {
                case NotificationStyle.List:
                    result.Add(new NotificationGroupView(g, g.Items.Select(i => new NotificationCardView(i, 0, 1, 0, 1, true)).ToList(), false, summary));
                    break;
                case NotificationStyle.Capsule:
                    result.Add(new NotificationGroupView(g,
                        expanded
                            ? g.Items.Select(i => new NotificationCardView(i, 0, 1, 0, 1, true)).ToList()
                            : [new NotificationCardView(g.Latest, 0, 1, 0, 1, false)],
                        !expanded, summary));
                    break;
                default:
                    if (expanded || g.Count == 1)
                    {
                        result.Add(new NotificationGroupView(g, g.Items.Select(i => new NotificationCardView(i, 0, 1, 0, 1, true)).ToList(), false, summary));
                    }
                    else
                    {
                        var cards = new List<NotificationCardView>();
                        for (int d = 0; d < Math.Min(g.Count, VisibleStackDepth + 1); d++)
                        {
                            // Каждая следующая карточка чуть меньше, ниже и прозрачнее: эффект колоды.
                            cards.Add(new NotificationCardView(g.Items[d], d, 1 - 0.05 * d, 8 * d, 1 - 0.25 * d, d == 0));
                        }
                        result.Add(new NotificationGroupView(g, cards, true, summary));
                    }
                    break;
            }
        }
        return result;
    }

    /// <summary>«Telegram · 3 новых · 5 мин назад».</summary>
    public static string Summary(NotificationGroup group, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(group);
        string count = group.Count == 1 ? "" : $" · {group.Count} {Plural(group.Count, "новое", "новых", "новых")}";
        return $"{group.AppName}{count} · {RelativeTime(group.LatestTime, now)}";
    }

    /// <summary>Относительное время по-русски.</summary>
    public static string RelativeTime(DateTimeOffset time, DateTimeOffset now)
    {
        var d = now - time;
        if (d < TimeSpan.FromMinutes(1)) return "сейчас";
        if (d < TimeSpan.FromHours(1))
        {
            int m = (int)d.TotalMinutes;
            return $"{m} мин назад";
        }
        if (d < TimeSpan.FromHours(24) && time.Date == now.Date)
            return time.ToString("HH:mm", CultureInfo.InvariantCulture);
        if (time.Date == now.Date.AddDays(-1)) return "вчера, " + time.ToString("HH:mm", CultureInfo.InvariantCulture);
        return time.ToString("d MMM", CultureInfo.GetCultureInfo("ru-RU"));
    }

    /// <summary>Русские формы множественного числа: 1 уведомление, 2 уведомления, 5 уведомлений.</summary>
    public static string Plural(int n, string one, string few, string many)
    {
        int n10 = Math.Abs(n) % 10, n100 = Math.Abs(n) % 100;
        if (n10 == 1 && n100 != 11) return one;
        if (n10 is >= 2 and <= 4 && (n100 < 12 || n100 > 14)) return few;
        return many;
    }
}
