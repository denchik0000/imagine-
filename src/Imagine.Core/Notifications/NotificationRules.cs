namespace Imagine.Core.Notifications;

public enum RuleAction { Hide, Silence, Highlight }

/// <summary>Правило: приложение и/или ключевое слово → действие.</summary>
public sealed record NotificationRule(string? AppIdContains, string? TextContains, RuleAction Action)
{
    public bool Matches(NotificationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (AppIdContains is null && TextContains is null) return false;
        if (AppIdContains is not null &&
            !item.AppId.Contains(AppIdContains, StringComparison.OrdinalIgnoreCase) &&
            !item.AppName.Contains(AppIdContains, StringComparison.OrdinalIgnoreCase)) return false;
        if (TextContains is not null &&
            !item.Title.Contains(TextContains, StringComparison.OrdinalIgnoreCase) &&
            !item.Body.Contains(TextContains, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }
}

/// <summary>Свой режим «Не беспокоить»: по расписанию или вручную.</summary>
public sealed record FocusSchedule
{
    public bool ManualOn { get; init; }
    public bool ScheduleEnabled { get; init; }
    public TimeOnly From { get; init; } = new(23, 0);
    public TimeOnly To { get; init; } = new(7, 0);
    /// <summary>Приложения, которым можно всплывать даже в режиме фокуса.</summary>
    public IReadOnlyList<string> AllowedApps { get; init; } = [];

    public bool IsActive(DateTimeOffset now)
    {
        if (ManualOn) return true;
        if (!ScheduleEnabled) return false;
        var t = TimeOnly.FromDateTime(now.DateTime);
        return From <= To ? t >= From && t < To : t >= From || t < To;
    }
}

/// <summary>Итог фильтрации: показать ли в шторке и можно ли всплыть баннером.</summary>
public readonly record struct FilterDecision(bool ShowInShade, bool AllowBanner, bool Highlighted);

public static class NotificationFilter
{
    public static FilterDecision Evaluate(NotificationItem item, IReadOnlyList<NotificationRule> rules, FocusSchedule focus, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(focus);
        bool show = true, banner = true, highlight = item.Priority == NotificationPriority.High;
        foreach (var rule in rules.Where(r => r.Matches(item)))
        {
            switch (rule.Action)
            {
                case RuleAction.Hide: show = false; banner = false; break;
                case RuleAction.Silence: banner = false; break;
                case RuleAction.Highlight: highlight = true; break;
            }
        }
        if (focus.IsActive(now) && !focus.AllowedApps.Any(a => item.AppId.Contains(a, StringComparison.OrdinalIgnoreCase) || item.AppName.Contains(a, StringComparison.OrdinalIgnoreCase)))
            banner = false;
        return new FilterDecision(show, banner && show, highlight);
    }
}
