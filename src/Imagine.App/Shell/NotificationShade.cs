using Imagine.App.Ui;
using Imagine.Color;
using Imagine.Core.Clock;
using Imagine.Core.Notifications;
using Imagine.Core.Services;
using Imagine.Core.Settings;
using Imagine.Core.Shades;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Imagine.App.Shell;

/// <summary>
/// Левая шторка: крупные часы, состояние (батарея, фокус, что играет) и уведомления
/// в одном из трёх видов ColorOS 17 — колода, капсулы или список.
/// Кнопки внутри чужих уведомлений Windows нажимать не даёт, поэтому карточки только читаются и смахиваются.
/// </summary>
public sealed class NotificationShade : ShadeWindow
{
    private readonly StackPanel _list = new() { Spacing = 10 };
    private readonly StackPanel _history = new() { Spacing = 6 };
    private readonly TextBlock _time = Ui.Ui.Text("", 64, 300);
    private readonly TextBlock _date = Ui.Ui.Text("", 15, 500);
    private readonly StackPanel _chips = Ui.Ui.HStack(8);
    private readonly HashSet<string> _expanded = [];
    private readonly Dictionary<uint, NotificationItem> _seen = [];
    private readonly DispatcherTimerLite _clock;
    private IReadOnlyList<NotificationItem> _items = [];
    private NotificationAccess _access = NotificationAccess.Unknown;
    private Expander? _historyExpander;

    public NotificationShade(AppServices services) : base(services, ShadeKind.Notifications)
    {
        Title = "Imagine — уведомления";
        _time.FontFamily = Ui.Ui.DisplayFont;
        _time.Foreground = Palette.Get(ColorRole.Primary);
        _time.LineHeight = 64;
        _date.Foreground = Palette.Get(ColorRole.OnSurface);

        var header = Ui.Ui.VStack(2, _time, _date, _chips);
        _chips.Margin = new Thickness(0, 10, 0, 4);

        var styleBar = Ui.Ui.HStack(4,
            StyleButton(NotificationStyle.Stack, "", "Колода"),
            StyleButton(NotificationStyle.Capsule, "", "Капсулы"),
            StyleButton(NotificationStyle.List, "", "Список"));
        var clearAll = new Button
        {
            Content = Ui.Ui.HStack(6, Ui.Ui.Icon(Glyphs.ClearAll, 13), Ui.Ui.Text("Очистить всё", 13)),
            HorizontalAlignment = HorizontalAlignment.Right,
            CornerRadius = new CornerRadius(14),
        };
        clearAll.Click += (_, _) => ClearAll();
        var toolbar = Ui.Ui.Grid("*,Auto", styleBar, clearAll);
        toolbar.Margin = new Thickness(0, 6, 0, 0);

        _historyExpander = new Expander
        {
            Header = Ui.Ui.HStack(8, Ui.Ui.Icon(Glyphs.History, 14), Ui.Ui.Text("Недавно закрытые", 13)),
            Content = _history,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
        };

        var scroll = new ScrollViewer
        {
            Content = Ui.Ui.VStack(10, _list, _historyExpander),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(0, 0, 6, 0),
        };
        var body = new Grid { Padding = new Thickness(20, 22, 14, 0), RowSpacing = 8 };
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        body.Children.Add(header);
        Grid.SetRow(toolbar, 1);
        body.Children.Add(toolbar);
        Grid.SetRow(scroll, 2);
        body.Children.Add(scroll);
        SetBody(body);

        _clock = new DispatcherTimerLite(DispatcherQueue, TimeSpan.FromSeconds(1), UpdateClock);
        Opened += (_, _) => _clock.Start();
        ClosedShade += (_, _) => _clock.Stop();
        services.Notifications.Changed += (_, _) => DispatcherQueue.TryEnqueue(async () =>
        {
            await LoadAsync();
            if (IsOpen) Render();
        });
    }

    private Button StyleButton(NotificationStyle style, string glyph, string tip)
    {
        var b = new Button { Content = Ui.Ui.Icon(glyph, 14), Padding = new Thickness(8, 6, 8, 6), CornerRadius = new CornerRadius(12) };
        ToolTipService.SetToolTip(b, tip);
        b.Click += (_, _) =>
        {
            Services.UpdateSettings(s => s with { Notifications = s.Notifications with { Style = style } });
            Render();
        };
        return b;
    }

    protected override async Task RefreshAsync()
    {
        UpdateClock();
        await LoadAsync();
        Render();
    }

    private async Task LoadAsync()
    {
        if (_access is NotificationAccess.Unknown) _access = await Services.Notifications.RequestAccessAsync();
        var items = _access == NotificationAccess.Allowed ? await Services.Notifications.GetAllAsync() : [];
        var now = DateTimeOffset.Now;
        // Пропавшие уведомления (смахнули здесь или в Центре уведомлений) кладём в историю.
        if (Services.Current.Notifications.KeepHistory)
        {
            var ids = items.Select(i => i.Id).ToHashSet();
            foreach (var gone in _seen.Values.Where(i => !ids.Contains(i.Id)).ToList()) Services.History.Add(gone, now);
        }
        _seen.Clear();
        foreach (var i in items) _seen[i.Id] = i;
        _items = items;
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        var style = Services.Current.Clock.Style;
        var (h, m, sec, ampm) = ClockFormatter.Digits(now, style);
        _time.Text = $"{h}:{m}" + (ampm is null ? "" : " " + ampm);
        _date.Text = ClockFormatter.LongDate(now);
    }

    private void Render()
    {
        var s = Services.Current.Notifications;
        var now = DateTimeOffset.Now;
        RenderChips(s);

        _list.Children.Clear();
        if (_access != NotificationAccess.Allowed)
        {
            _list.Children.Add(AccessCard());
            return;
        }
        var visible = _items.Select(i => (Item: i, Decision: NotificationFilter.Evaluate(i, s.Rules, s.Focus, now)))
            .Where(x => x.Decision.ShowInShade).ToList();
        var highlighted = visible.Where(x => x.Decision.Highlighted).Select(x => x.Item.Id).ToHashSet();
        if (visible.Count == 0)
        {
            _list.Children.Add(Empty());
        }
        else
        {
            foreach (var group in NotificationArranger.Arrange(visible.Select(v => v.Item), s.Style, _expanded, now))
                _list.Children.Add(GroupView(group, s.Style, highlighted, now));
        }

        _history.Children.Clear();
        var history = Services.History.Snapshot(now);
        _historyExpander!.Visibility = history.Count > 0 && s.KeepHistory ? Visibility.Visible : Visibility.Collapsed;
        foreach (var h in history.Take(30))
            _history.Children.Add(CardView(h, now, compact: true, highlighted: false, dismissible: false));
    }

    private void RenderChips(NotificationSettings s)
    {
        _chips.Children.Clear();
        if (Services.Power.GetBattery() is { } b)
            _chips.Children.Add(Chip(b.IsCharging ? "" : Glyphs.Battery, $"{b.Percent}%" + (b.Remaining is { } r && !b.IsCharging ? $" · {Math.Floor(r.TotalHours)} ч {r.Minutes} мин" : b.IsCharging ? " · заряжается" : "")));
        if (s.Focus.IsActive(DateTimeOffset.Now)) _chips.Children.Add(Chip(Glyphs.Focus, "Фокус"));
        if (Services.Media.Current is { IsPlaying: true } m) _chips.Children.Add(Chip(Glyphs.Music, $"{m.Title}"));
        if (Palette.Current is { } t) _chips.Children.Add(Chip(Glyphs.Palette, ColorUtils.ToHex(t.Seed)));
    }

    private static Border Chip(string glyph, string text) => new()
    {
        CornerRadius = new CornerRadius(12),
        Padding = new Thickness(10, 4, 10, 4),
        Background = Palette.Get(ColorRole.SecondaryContainer, 0.8),
        Child = Ui.Ui.HStack(6, Ui.Ui.Icon(glyph, 12, Palette.Get(ColorRole.OnSecondaryContainer)), Ui.Ui.Text(text, 12, 500, Palette.Get(ColorRole.OnSecondaryContainer)).With(t => { t.MaxWidth = 180; t.TextTrimming = TextTrimming.CharacterEllipsis; t.TextWrapping = TextWrapping.NoWrap; })),
    };

    private UIElement Empty() => new StackPanel
    {
        Spacing = 8,
        Margin = new Thickness(0, 40, 0, 0),
        HorizontalAlignment = HorizontalAlignment.Center,
        Children =
        {
            Ui.Ui.Icon(Glyphs.Bell, 40, Palette.Get(ColorRole.OnSurfaceVariant, 0.5)),
            Ui.Ui.Text("Нет новых уведомлений", 14, 500, Palette.Get(ColorRole.OnSurfaceVariant)).With(t => t.HorizontalAlignment = HorizontalAlignment.Center),
        },
    };

    private UIElement AccessCard()
    {
        string text = _access == NotificationAccess.Unsupported
            ? "Чтение уведомлений Windows доступно только в версии MSIX (пакет из релиза). В обычной сборке шторка показывает часы и состояние."
            : "Разрешите Imagine читать уведомления: Параметры → Конфиденциальность → Уведомления.";
        var open = Ui.Ui.Button("Открыть параметры", () => Services.Actions.OpenUri("ms-settings:privacy-notifications"), Glyphs.Settings);
        var retry = Ui.Ui.Button("Проверить снова", async () =>
        {
            _access = await Services.Notifications.RequestAccessAsync();
            await LoadAsync();
            Render();
        });
        return Card(Ui.Ui.VStack(10, Ui.Ui.Text(text, 13, 400, Palette.Get(ColorRole.OnSurface)), Ui.Ui.HStack(8, open, retry)));
    }

    private UIElement GroupView(NotificationGroupView group, NotificationStyle style, HashSet<uint> highlighted, DateTimeOffset now)
    {
        var g = group.Group;
        if (style == NotificationStyle.Capsule && group.Collapsed)
        {
            // Капсула: значок, приложение, сводка. Нажатие раскрывает.
            var capsule = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(22),
                Padding = new Thickness(8, 6, 14, 6),
                Background = Palette.Get(highlighted.Contains(g.Latest.Id) ? ColorRole.PrimaryContainer : ColorRole.SurfaceContainerHigh, 0.7),
                BorderThickness = new Thickness(0),
                Content = Ui.Ui.Grid("Auto,*,Auto",
                    AppIcon(g.AppIconPath, 28),
                    Ui.Ui.VStack(0,
                        Ui.Ui.Text(g.AppName, 13, 600, Palette.Get(ColorRole.OnSurface)),
                        Ui.Ui.Text(group.Summary, 12, 400, Palette.Get(ColorRole.OnSurfaceVariant)).With(t => { t.TextTrimming = TextTrimming.CharacterEllipsis; t.TextWrapping = TextWrapping.NoWrap; })),
                    CountBadge(g.Count)),
            };
            capsule.Click += (_, _) => ToggleExpanded(g.AppId);
            return capsule;
        }

        var panel = new StackPanel { Spacing = 6 };
        var header = Ui.Ui.Grid("Auto,*,Auto",
            AppIcon(g.AppIconPath, 18),
            Ui.Ui.Text(g.AppName + (g.Count > 1 ? $" · {g.Count}" : ""), 12, 600, Palette.Get(ColorRole.OnSurfaceVariant)),
            g.Count > 1
                ? new HyperlinkButton { Content = _expanded.Contains(g.AppId) ? "Свернуть" : "Развернуть", Padding = new Thickness(4, 0, 4, 0), FontSize = 12 }.With(b => b.Click += (_, _) => ToggleExpanded(g.AppId))
                : new Border());
        header.Margin = new Thickness(4, 0, 0, 0);
        panel.Children.Add(header);

        if (group.Collapsed && style == NotificationStyle.Stack)
        {
            // Колода: карточки накладываются, задние меньше и прозрачнее.
            var deck = new Grid();
            foreach (var card in group.Cards.Reverse())
            {
                var view = CardView(card.Item, now, compact: false, highlighted: highlighted.Contains(card.Item.Id), dismissible: card.DepthInStack == 0);
                view.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 1);
                view.RenderTransform = new CompositeTransform { ScaleX = card.Scale, ScaleY = card.Scale, TranslateY = card.OffsetY };
                view.Opacity = card.Opacity;
                if (card.DepthInStack > 0) view.IsHitTestVisible = false;
                deck.Children.Add(view);
            }
            deck.Margin = new Thickness(0, 0, 0, 8 * (group.Cards.Count - 1));
            var tap = new Button { Content = deck, Padding = new Thickness(0), Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            tap.Click += (_, _) => ToggleExpanded(g.AppId);
            panel.Children.Add(tap);
        }
        else
        {
            foreach (var card in group.Cards)
                panel.Children.Add(CardView(card.Item, now, compact: false, highlighted: highlighted.Contains(card.Item.Id), dismissible: true));
        }
        return panel;
    }

    private void ToggleExpanded(string appId)
    {
        if (!_expanded.Remove(appId)) _expanded.Add(appId);
        Render();
    }

    private Border CardView(NotificationItem item, DateTimeOffset now, bool compact, bool highlighted, bool dismissible)
    {
        var texts = new StackPanel { Spacing = 2 };
        var top = Ui.Ui.Grid("*,Auto",
            Ui.Ui.Text(item.Title.Length > 0 ? item.Title : item.AppName, compact ? 13 : 14, 600, Palette.Get(ColorRole.OnSurface)).With(t => { t.TextTrimming = TextTrimming.CharacterEllipsis; t.TextWrapping = TextWrapping.NoWrap; }),
            Ui.Ui.Text(NotificationArranger.RelativeTime(item.Timestamp, now), 11, 400, Palette.Get(ColorRole.OnSurfaceVariant)));
        texts.Children.Add(top);
        if (item.Body.Length > 0)
            texts.Children.Add(Ui.Ui.Text(item.Body, compact ? 12 : 13, 400, Palette.Get(ColorRole.OnSurfaceVariant)).With(t => { t.MaxLines = compact ? 1 : 4; t.TextTrimming = TextTrimming.CharacterEllipsis; }));
        if (!compact && item.ImagePath is { } img && File.Exists(img))
            texts.Children.Add(new Border { CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 6, 0, 0), MaxHeight = 160, Child = new Image { Source = Ui.Ui.Thumbnail(img, 400), Stretch = Stretch.UniformToFill } });

        UIElement trailing = new Border();
        if (dismissible)
        {
            var close = new Button { Content = Ui.Ui.Icon(Glyphs.Close, 10), Padding = new Thickness(6), CornerRadius = new CornerRadius(12), Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0), VerticalAlignment = VerticalAlignment.Top };
            ToolTipService.SetToolTip(close, "Смахнуть");
            close.Click += (_, _) => Dismiss(item);
            trailing = close;
        }
        var grid = Ui.Ui.Grid("Auto,*,Auto", AppIcon(item.AppIconPath, compact ? 20 : 32).With(i => i.VerticalAlignment = VerticalAlignment.Top), texts, trailing);
        var card = Card(grid, compact ? 14 : 20, compact ? 10 : 14);
        if (highlighted)
        {
            card.BorderBrush = Palette.Get(ColorRole.Primary);
            card.BorderThickness = new Thickness(1.5);
        }
        return card;
    }

    private static FrameworkElement AppIcon(string? path, double size)
    {
        if (path is not null && File.Exists(path))
            return new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size / 4), Child = new Image { Source = Ui.Ui.Thumbnail(path, 64), Stretch = Stretch.UniformToFill } };
        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 2),
            Background = Palette.Get(ColorRole.TertiaryContainer),
            Child = Ui.Ui.Icon(Glyphs.Bell, size * 0.5, Palette.Get(ColorRole.OnTertiaryContainer)).With(i => { i.HorizontalAlignment = HorizontalAlignment.Center; i.VerticalAlignment = VerticalAlignment.Center; }),
        };
    }

    private static Border CountBadge(int count) => new()
    {
        MinWidth = 24,
        Height = 24,
        CornerRadius = new CornerRadius(12),
        Background = Palette.Get(ColorRole.Primary),
        Child = Ui.Ui.Text(count.ToString(System.Globalization.CultureInfo.InvariantCulture), 12, 600, Palette.Get(ColorRole.OnPrimary)).With(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; t.Margin = new Thickness(6, 0, 6, 0); }),
        VerticalAlignment = VerticalAlignment.Center,
    };

    private void Dismiss(NotificationItem item)
    {
        Services.Notifications.Remove(item.Id);
        if (Services.Current.Notifications.KeepHistory) Services.History.Add(item, DateTimeOffset.Now);
        _seen.Remove(item.Id);
        _items = _items.Where(i => i.Id != item.Id).ToList();
        Render();
    }

    private void ClearAll()
    {
        var now = DateTimeOffset.Now;
        if (Services.Current.Notifications.KeepHistory)
            foreach (var i in _items) Services.History.Add(i, now);
        Services.Notifications.ClearAll();
        _seen.Clear();
        _items = [];
        Render();
    }
}

/// <summary>Таймер очереди диспетчера с простым API.</summary>
public sealed class DispatcherTimerLite
{
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _timer;

    public DispatcherTimerLite(Microsoft.UI.Dispatching.DispatcherQueue queue, TimeSpan interval, Action tick)
    {
        _timer = queue.CreateTimer();
        _timer.Interval = interval;
        _timer.Tick += (_, _) => tick();
    }

    public TimeSpan Interval
    {
        get => _timer.Interval;
        set => _timer.Interval = value;
    }

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();
}
