using Imagine.App.Ui;
using Imagine.Color;
using Imagine.Core.Clock;
using Imagine.Core.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using static Imagine.App.Pages.PageHelpers;

namespace Imagine.App.Pages;

/// <summary>Часы на рабочем столе и экране блокировки: раскладка, шрифт, цвет, место, «переезд» после входа.</summary>
internal static class ClockPage
{
    public static UIElement Build(AppServices services)
    {
        var c = services.Current.Clock;
        var st = c.Style;
        var preview = new ClockView();
        var stage = new Grid { Height = 240, CornerRadius = new CornerRadius(16), Padding = new Thickness(28) };
        var path = services.Current.Wallpaper.Path ?? services.Wallpapers.GetWallpaper();
        if (path is not null && File.Exists(path))
            stage.Children.Add(new Image { Source = Ui.Ui.Thumbnail(path, 1000), Stretch = Stretch.UniformToFill, Margin = new Thickness(-28) });
        else stage.Background = Palette.Get(ColorRole.PrimaryContainer);
        stage.Children.Add(preview);

        void Refresh()
        {
            var s = services.Current.Clock.Style;
            preview.HorizontalAlignment = s.Alignment switch { ClockAlignment.Center => HorizontalAlignment.Center, ClockAlignment.Right => HorizontalAlignment.Right, _ => HorizontalAlignment.Left };
            preview.VerticalAlignment = s.Placement is ClockPlacement.BottomLeft or ClockPlacement.BottomRight ? VerticalAlignment.Bottom : s.Placement == ClockPlacement.Center ? VerticalAlignment.Center : VerticalAlignment.Top;
            bool topLight = services.Engine.CurrentAnalysis?.TopIsLight ?? false;
            var role = s.ColorRole ?? (topLight ? ColorRole.OnPrimaryFixedVariant : ColorRole.PrimaryFixed);
            preview.Configure(s, 180 * Math.Clamp(s.RelativeSize, 0.05, 0.4) / 0.16 * 0.5, Palette.Get(role), Palette.Get(topLight ? ColorRole.OnSecondaryFixedVariant : ColorRole.SecondaryFixed), Palette.Current?[ColorRole.Primary] ?? 0xFFD55E5D);
        }
        Refresh();
        var timer = new Shell.DispatcherTimerLite(services.Dispatcher, TimeSpan.FromSeconds(1), () => preview.Tick(DateTime.Now));
        timer.Start();
        stage.Unloaded += (_, _) => timer.Stop();

        void Up(Func<ClockSettings, ClockSettings> f) { services.UpdateSettings(x => x with { Clock = f(x.Clock) }); Refresh(); }
        void Style(Func<ClockStyle, ClockStyle> f) => Up(x => x with { Style = f(x.Style) });

        var fonts = new[] { "Segoe UI Variable Display", "Segoe UI Variable Text", "Bahnschrift", "Segoe UI", "Cascadia Code", "Georgia", "Candara", "Corbel", "Consolas" };
        var roleItems = new (ColorRole?, string)[]
        {
            (null, "Автоматически по обоям"),
            (ColorRole.Primary, "Основной"),
            (ColorRole.PrimaryFixed, "Основной светлый"),
            (ColorRole.Tertiary, "Третичный"),
            (ColorRole.TertiaryFixed, "Третичный светлый"),
            (ColorRole.Secondary, "Вторичный"),
            (ColorRole.InverseSurface, "Контрастный"),
            (ColorRole.OnSurface, "Как текст"),
        };

        return Page("Часы", "Крупные часы в цвет палитры. После входа они «переезжают» с экрана блокировки на рабочий стол.",
            stage,
            SettingsCard.Group("Часы на рабочем столе",
                Toggle(Glyphs.Clock, "Показывать часы", "Встраиваются под значки рабочего стола", c.DesktopClock, v => Up(x => x with { DesktopClock = v })),
                Toggle(Glyphs.Sparkle, "«Переезд» после разблокировки", "Часы вылетают из центра экрана на своё место", c.FlyInAfterUnlock, v => Up(x => x with { FlyInAfterUnlock = v }))),
            SettingsCard.Group("Вид",
                Card(Glyphs.Clock, "Раскладка", null, Ui.Ui.Combo(new[] { (ClockLayout.Stacked, "Столбиком"), (ClockLayout.Inline, "В строку"), (ClockLayout.Editorial, "Журнальная"), (ClockLayout.Analog, "Стрелочные") }, st.Layout, v => Style(x => x with { Layout = v }))),
                Card(Glyphs.Crop, "Место", null, Ui.Ui.Combo(new[] { (ClockPlacement.TopLeft, "Слева сверху"), (ClockPlacement.TopCenter, "Сверху по центру"), (ClockPlacement.TopRight, "Справа сверху"), (ClockPlacement.Center, "В центре"), (ClockPlacement.BottomLeft, "Слева снизу"), (ClockPlacement.BottomRight, "Справа снизу") }, st.Placement, v => Style(x => x with { Placement = v }))),
                Card(Glyphs.Crop, "Выравнивание", null, Ui.Ui.Combo(new[] { (ClockAlignment.Left, "По левому краю"), (ClockAlignment.Center, "По центру"), (ClockAlignment.Right, "По правому краю") }, st.Alignment, v => Style(x => x with { Alignment = v }))),
                Card(Glyphs.Sun, "Размер", "Доля высоты экрана", Ui.Ui.Slider(0.06, 0.32, st.RelativeSize, v => Style(x => x with { RelativeSize = v }), 0.01)),
                Card(Glyphs.Code, "Шрифт", null, Ui.Ui.Combo(fonts.Select(f => (f, f)), st.FontFamily, v => Style(x => x with { FontFamily = v }))),
                Card(Glyphs.Code, "Насыщенность", "От тонкого до жирного", Ui.Ui.Slider(100, 900, st.FontWeight, v => Style(x => x with { FontWeight = (int)v }), 100)),
                Card(Glyphs.Palette, "Цвет", null, Ui.Ui.Combo(roleItems, st.ColorRole, v => Style(x => x with { ColorRole = v }))),
                Toggle(Glyphs.Light, "Свечение цифр", null, st.Glow, v => Style(x => x with { Glow = v })),
                Toggle(Glyphs.Clock, "24-часовой формат", null, st.Use24Hour, v => Style(x => x with { Use24Hour = v })),
                Toggle(Glyphs.Clock, "Секунды", null, st.ShowSeconds, v => Style(x => x with { ShowSeconds = v })),
                Toggle(Glyphs.Calendar, "Дата", null, st.ShowDate, v => Style(x => x with { ShowDate = v }))));
    }
}
