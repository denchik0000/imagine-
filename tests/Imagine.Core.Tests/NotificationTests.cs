using Imagine.Core.Notifications;

namespace Imagine.Core.Tests;

public class NotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 21, 0, 0, TimeSpan.FromHours(3));

    private static NotificationItem N(uint id, string app, int minutesAgo, string title = "t", NotificationPriority p = NotificationPriority.Normal)
        => new() { Id = id, AppId = app, AppName = app, Title = title, Timestamp = Now.AddMinutes(-minutesAgo), Priority = p };

    [Fact]
    public void Groups_by_app_newest_first()
    {
        var groups = NotificationArranger.Group([N(1, "Mail", 30), N(2, "Telegram", 5), N(3, "Mail", 1)]);
        Assert.Equal("Mail", groups[0].AppId);
        Assert.Equal(3u, groups[0].Latest.Id);
        Assert.Equal(2, groups[0].Count);
    }

    [Fact]
    public void High_priority_groups_come_first()
    {
        var groups = NotificationArranger.Group([N(1, "Bank", 50, p: NotificationPriority.High), N(2, "Chat", 1)]);
        Assert.Equal("Bank", groups[0].AppId);
    }

    [Fact]
    public void Stack_style_builds_a_deck()
    {
        var items = Enumerable.Range(1, 5).Select(i => N((uint)i, "Chat", i)).ToList();
        var v = NotificationArranger.Arrange(items, NotificationStyle.Stack, now: Now).Single();
        Assert.True(v.Collapsed);
        Assert.Equal(3, v.Cards.Count);
        Assert.True(v.Cards[1].Scale < v.Cards[0].Scale);
        Assert.True(v.Cards[2].Opacity < v.Cards[1].Opacity);
    }

    [Fact]
    public void Expanded_stack_and_list_show_everything()
    {
        var items = Enumerable.Range(1, 5).Select(i => N((uint)i, "Chat", i)).ToList();
        Assert.Equal(5, NotificationArranger.Arrange(items, NotificationStyle.Stack, new HashSet<string> { "Chat" }, Now).Single().Cards.Count);
        Assert.Equal(5, NotificationArranger.Arrange(items, NotificationStyle.List, now: Now).Single().Cards.Count);
        Assert.Single(NotificationArranger.Arrange(items, NotificationStyle.Capsule, now: Now).Single().Cards);
    }

    [Theory]
    [InlineData(1, "новое")]
    [InlineData(2, "новых")]
    [InlineData(5, "новых")]
    [InlineData(11, "новых")]
    [InlineData(21, "новое")]
    public void Russian_plural(int n, string expected)
        => Assert.Equal(expected, NotificationArranger.Plural(n, "новое", "новых", "новых"));

    [Fact]
    public void Plural_forms_for_few()
    {
        Assert.Equal("уведомления", NotificationArranger.Plural(3, "уведомление", "уведомления", "уведомлений"));
        Assert.Equal("уведомлений", NotificationArranger.Plural(12, "уведомление", "уведомления", "уведомлений"));
        Assert.Equal("уведомление", NotificationArranger.Plural(101, "уведомление", "уведомления", "уведомлений"));
    }

    [Fact]
    public void Relative_time_in_russian()
    {
        Assert.Equal("сейчас", NotificationArranger.RelativeTime(Now, Now));
        Assert.Equal("5 мин назад", NotificationArranger.RelativeTime(Now.AddMinutes(-5), Now));
        Assert.Equal("18:30", NotificationArranger.RelativeTime(Now.AddHours(-2.5), Now));
        Assert.StartsWith("вчера", NotificationArranger.RelativeTime(Now.AddDays(-1), Now));
    }

    [Fact]
    public void Rules_hide_silence_and_highlight()
    {
        var rules = new List<NotificationRule>
        {
            new("Promo", null, RuleAction.Hide),
            new(null, "код", RuleAction.Highlight),
            new("Chat", null, RuleAction.Silence),
        };
        var focus = new FocusSchedule();
        Assert.False(NotificationFilter.Evaluate(N(1, "PromoShop", 1), rules, focus, Now).ShowInShade);
        var chat = NotificationFilter.Evaluate(N(2, "Chat", 1, "Ваш код 1234"), rules, focus, Now);
        Assert.True(chat.ShowInShade);
        Assert.False(chat.AllowBanner);
        Assert.True(chat.Highlighted);
    }

    [Fact]
    public void Focus_schedule_wraps_midnight_and_allows_exceptions()
    {
        var focus = new FocusSchedule { ScheduleEnabled = true, From = new TimeOnly(23, 0), To = new TimeOnly(7, 0), AllowedApps = ["Phone"] };
        var night = new DateTimeOffset(2026, 11, 16, 2, 0, 0, TimeSpan.Zero);
        var day = new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);
        Assert.True(focus.IsActive(night));
        Assert.False(focus.IsActive(day));
        Assert.False(NotificationFilter.Evaluate(N(1, "Chat", 0), [], focus, night).AllowBanner);
        Assert.True(NotificationFilter.Evaluate(N(2, "Phone", 0), [], focus, night).AllowBanner);
    }

    [Fact]
    public void History_respects_capacity_and_retention()
    {
        var h = new NotificationHistory(capacity: 2, retention: TimeSpan.FromHours(1));
        h.Add(N(1, "A", 0), Now.AddHours(-2));
        h.Add(N(2, "A", 0), Now);
        h.Add(N(3, "A", 0), Now);
        var snap = h.Snapshot(Now);
        Assert.Equal(new uint[] { 3, 2 }, snap.Select(x => x.Id));
    }
}
