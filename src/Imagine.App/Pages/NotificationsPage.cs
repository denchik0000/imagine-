using Imagine.App.Ui;
using Imagine.Core.Notifications;
using Imagine.Core.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static Imagine.App.Pages.PageHelpers;

namespace Imagine.App.Pages;

/// <summary>Вид уведомлений, история, правила и режим «Фокус».</summary>
internal static class NotificationsPage
{
    public static UIElement Build(AppServices services)
    {
        var n = services.Current.Notifications;
        void Up(Func<NotificationSettings, NotificationSettings> f) => services.UpdateSettings(x => x with { Notifications = f(x.Notifications) });
        void Focus(Func<FocusSchedule, FocusSchedule> f) => Up(x => x with { Focus = f(x.Focus) });

        var access = new StackPanel();
        async void CheckAccess()
        {
            var a = await services.Notifications.RequestAccessAsync();
            access.Children.Clear();
            access.Children.Add(a switch
            {
                Core.Services.NotificationAccess.Allowed => Info("Доступ к уведомлениям есть", "Шторка показывает уведомления всех приложений. Кнопки внутри чужих уведомлений Windows нажимать не позволяет — их можно читать и смахивать.", InfoBarSeverity.Success),
                Core.Services.NotificationAccess.Unsupported => Info("Нужна версия MSIX", "Windows отдаёт уведомления только упакованным приложениям. Установите Imagine из MSIX-пакета (см. README) — остальное работает и так.", InfoBarSeverity.Warning),
                _ => Info("Доступ не выдан", "Разрешите в Параметрах → Конфиденциальность и защита → Уведомления.", InfoBarSeverity.Warning).With(i => i.ActionButton = Ui.Ui.Button("Открыть параметры", () => services.Actions.OpenUri("ms-settings:privacy-notifications"))),
            });
        }
        CheckAccess();

        var style = Ui.Ui.Radios(new[]
        {
            (NotificationStyle.Stack, "Колода — уведомления приложения лежат стопкой, сверху новое"),
            (NotificationStyle.Capsule, "Капсулы — компактные «пилюли», раскрываются нажатием"),
            (NotificationStyle.List, "Список — всё подряд, как обычно"),
        }, n.Style, v => Up(x => x with { Style = v }));

        var rules = new StackPanel { Spacing = 6 };
        void RenderRules()
        {
            rules.Children.Clear();
            var list = services.Current.Notifications.Rules.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                int index = i;
                var r = list[i];
                void Save(NotificationRule nr) { var copy = services.Current.Notifications.Rules.ToList(); copy[index] = nr; Up(x => x with { Rules = copy }); }
                var app = Ui.Ui.TextBox(r.AppIdContains ?? "", v => Save(r with { AppIdContains = string.IsNullOrWhiteSpace(v) ? null : v }), "Приложение содержит…", 180);
                var text = Ui.Ui.TextBox(r.TextContains ?? "", v => Save(r with { TextContains = string.IsNullOrWhiteSpace(v) ? null : v }), "Текст содержит…", 180);
                var action = Ui.Ui.Combo(new[] { (RuleAction.Hide, "Скрывать"), (RuleAction.Silence, "Без баннера"), (RuleAction.Highlight, "Выделять") }, r.Action, v => Save(r with { Action = v }), 150);
                var del = new Button { Content = Ui.Ui.Icon(Glyphs.Delete, 12) };
                del.Click += (_, _) => { var copy = services.Current.Notifications.Rules.ToList(); copy.RemoveAt(index); Up(x => x with { Rules = copy }); RenderRules(); };
                rules.Children.Add(Ui.Ui.HStack(8, app, text, action, del));
            }
            rules.Children.Add(Ui.Ui.Button("Добавить правило", () =>
            {
                Up(x => x with { Rules = [.. x.Rules, new NotificationRule(null, null, RuleAction.Highlight)] });
                RenderRules();
            }, Glyphs.Add));
        }
        RenderRules();

        var allowed = Ui.Ui.TextBox(string.Join(", ", n.Focus.AllowedApps), v => Focus(f => f with { AllowedApps = v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) }), "Telegram, Outlook", 260);

        return Page("Уведомления", "Как на экране блокировки ColorOS 17: три вида, история смахнутых, правила и «Фокус».",
            access,
            SettingsCard.Group("Вид", Card(Glyphs.Bell, "Как группировать", null, null, style)),
            SettingsCard.Group("История",
                Toggle(Glyphs.History, "Хранить недавно закрытые", "Внизу шторки появится «Недавно закрытые» — сутки, до 200 штук", n.KeepHistory, v => Up(x => x with { KeepHistory = v })),
                Card(Glyphs.Delete, "Очистить историю", null, Ui.Ui.Button("Очистить", () => services.History.Clear()))),
            SettingsCard.Group("Фокус",
                Toggle(Glyphs.Focus, "Фокус сейчас", "Уведомления копятся в шторке молча, кроме разрешённых приложений", n.Focus.ManualOn, v => Focus(f => f with { ManualOn = v })),
                Toggle(Glyphs.Calendar, "По расписанию", null, n.Focus.ScheduleEnabled, v => Focus(f => f with { ScheduleEnabled = v })),
                Card(Glyphs.Clock, "С и до", null, Ui.Ui.HStack(8, Time(n.Focus.From, v => Focus(f => f with { From = v })), Time(n.Focus.To, v => Focus(f => f with { To = v })))),
                Card(Glyphs.Pin, "Всегда пропускать", "Через запятую, по имени приложения", allowed)),
            SettingsCard.Group("Правила", Card(Glyphs.Settings, "Свои правила", "Скрыть рекламу магазина, выделить сообщения от начальника — что угодно по названию приложения или тексту", null, rules)));
    }
}
