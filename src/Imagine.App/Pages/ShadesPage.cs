using Imagine.App.Shell;
using Imagine.App.Ui;
using Imagine.Core.Hotkeys;
using Imagine.Core.Settings;
using Imagine.Core.Shades;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using static Imagine.App.Pages.PageHelpers;

namespace Imagine.App.Pages;

/// <summary>Две шторки: как открывать (край, тачпад, клавиши), где граница, какие плитки и в каком порядке.</summary>
internal static class ShadesPage
{
    public static UIElement Build(AppServices services)
    {
        var s = services.Current.Shades;
        var e = s.Edge;
        void Up(Func<ShadeSettings, ShadeSettings> f) => services.UpdateSettings(x => x with { Shades = f(x.Shades) });
        void Edge(Func<EdgeSettings, EdgeSettings> f) => Up(x => x with { Edge = f(x.Edge) });

        var tiles = new StackPanel { Spacing = 4 };
        void RenderTiles()
        {
            tiles.Children.Clear();
            var layout = new QuickTileLayout(services.Current.Shades.Tiles, services.Current.Shades.TileColumns);
            int i = 0;
            foreach (var slot in layout.Slots)
            {
                var info = QuickTileCatalog.Get(slot.Id);
                int index = i++;
                var id = slot.Id;
                void Save(QuickTileLayout l) { Up(x => x with { Tiles = l.Slots.ToList(), TileColumns = l.Columns }); RenderTiles(); }
                var up = new Button { Content = Ui.Ui.Icon(Glyphs.ChevronUp, 12), IsEnabled = index > 0 };
                up.Click += (_, _) => Save(layout.Move(id, index - 1));
                var down = new Button { Content = Ui.Ui.Icon(Glyphs.ChevronDown, 12), IsEnabled = index < layout.Slots.Count - 1 };
                down.Click += (_, _) => Save(layout.Move(id, index + 1));
                var wide = new ToggleButton { Content = "Широкая", IsChecked = slot.Size == QuickTileSize.Wide };
                wide.Click += (_, _) => Save(layout.Resize(id, wide.IsChecked == true ? QuickTileSize.Wide : QuickTileSize.Compact));
                var remove = new Button { Content = Ui.Ui.Icon(Glyphs.Delete, 12) };
                ToolTipService.SetToolTip(remove, "Убрать");
                remove.Click += (_, _) => Save(layout.Remove(id));
                tiles.Children.Add(new Border
                {
                    Padding = new Thickness(12, 6, 8, 6),
                    CornerRadius = new CornerRadius(6),
                    Background = Ui.Ui.ThemeBrush("CardBackgroundFillColorSecondaryBrush"),
                    Child = Ui.Ui.Grid("Auto,*,Auto", Ui.Ui.Icon(info.Glyph, 16), Ui.Ui.Text(info.Label).With(t => t.VerticalAlignment = VerticalAlignment.Center), Ui.Ui.HStack(4, wide, up, down, remove)),
                });
            }
            var available = layout.Available.ToList();
            if (available.Count > 0)
            {
                var add = new DropDownButton { Content = Ui.Ui.HStack(8, Ui.Ui.Icon(Glyphs.Add, 12), Ui.Ui.Text("Добавить плитку")) };
                var menu = new MenuFlyout();
                foreach (var id in available)
                {
                    var info = QuickTileCatalog.Get(id);
                    var item = new MenuFlyoutItem { Text = info.Label, Icon = new FontIcon { Glyph = info.Glyph, FontFamily = Ui.Ui.IconFont } };
                    item.Click += (_, _) => { Up(x => x with { Tiles = layout.Add(id).Slots.ToList() }); RenderTiles(); };
                    menu.Items.Add(item);
                }
                add.Flyout = menu;
                add.Margin = new Thickness(0, 6, 0, 0);
                tiles.Children.Add(add);
            }
        }
        RenderTiles();

        TextBox Hotkey(string value, Action<string> save)
        {
            var box = Ui.Ui.TextBox(value, _ => { }, "Например, Win+Alt+N", 180);
            var status = Ui.Ui.Caption("");
            box.LostFocus += (_, _) =>
            {
                if (HotkeyGesture.TryParse(box.Text, out var g) && !g.IsEmpty) { save(g.ToString()); box.Text = g.ToString(); }
                else box.Text = value;
            };
            return box;
        }

        return Page("Шторки", "Слева — уведомления и состояние, справа — быстрые настройки и медиа. Открываются мышью у верхнего края, тачпадом или клавишами.",
            Toggle(Glyphs.Shade, "Шторки включены", null, s.Enabled, v => Up(x => x with { Enabled = v })),
            SettingsCard.Group("Верхний край экрана",
                Toggle(Glyphs.Mouse, "Открывать мышью у края", "Задержите курсор у верхнего края или «толкните» его вверх", e.MouseEnabled, v => Edge(x => x with { MouseEnabled = v })),
                Card(Glyphs.Clock, "Задержка у края", "Миллисекунды до открытия; больше — меньше случайных срабатываний", Ui.Ui.Number(e.DwellMs, 0, 2000, v => Edge(x => x with { DwellMs = (int)v }), 20)),
                Card(Glyphs.ChevronUp, "Сила «толчка»", "Сколько движений вверх у края открывают шторку сразу", Ui.Ui.Number(e.PushEvents, 1, 30, v => Edge(x => x with { PushEvents = (int)v }))),
                Card(Glyphs.Shade, "Граница между шторками", "Доля ширины экрана: левее — уведомления, правее — быстрые настройки", Ui.Ui.Slider(0.2, 0.8, e.SplitRatio, v => Edge(x => x with { SplitRatio = v }), 0.05)),
                Card(Glyphs.Crop, "Мёртвые зоны по углам", "Чтобы не мешать кнопкам окон: слева и справа, пиксели",
                    Ui.Ui.HStack(8, Ui.Ui.Number(e.LeftDeadZone, 0, 800, v => Edge(x => x with { LeftDeadZone = (int)v }), 8), Ui.Ui.Number(e.RightDeadZone, 0, 800, v => Edge(x => x with { RightDeadZone = (int)v }), 8))),
                Toggle(Glyphs.Shuffle, "Поменять стороны", "Уведомления справа, быстрые настройки слева", e.SwapSides, v => Edge(x => x with { SwapSides = v })),
                Toggle(Glyphs.Picture, "Не открывать в полноэкранных приложениях", "Игры, видео, презентации", e.DisableInFullscreen, v => Edge(x => x with { DisableInFullscreen = v })),
                Card(Glyphs.Clock, "Пауза после закрытия", "мс; чтобы шторка не открылась снова сразу", Ui.Ui.Number(e.CooldownMs, 0, 3000, v => Edge(x => x with { CooldownMs = (int)v }), 50))),
            SettingsCard.Group("Тачпад",
                Toggle(Glyphs.Touchpad, "Потянуть двумя пальцами у края", "Курсор у верхнего края + прокрутка двумя пальцами вниз", e.TouchpadPullEnabled, v => Edge(x => x with { TouchpadPullEnabled = v })),
                Card(Glyphs.Touchpad, "Чувствительность", "Сколько нужно прокрутить; меньше — чувствительнее", Ui.Ui.Number(e.WheelPullThreshold, 60, 2000, v => Edge(x => x with { WheelPullThreshold = (int)v }), 20)),
                Card(Glyphs.Rotate, "Направление", "Если шторка открывается при движении не в ту сторону — поменяйте", Ui.Ui.Combo(new[] { (WheelPullDirection.Positive, "Обычное"), (WheelPullDirection.Negative, "Обратное"), (WheelPullDirection.Any, "Любое") }, e.WheelDirection, v => Edge(x => x with { WheelDirection = v }))),
                Info("Свайп тремя или четырьмя пальцами", "Windows не отдаёт эти жесты программам, но их можно назначить на горячие клавиши Imagine: Параметры → Bluetooth и устройства → Сенсорная панель → Расширенные жесты → «Пользовательское сочетание клавиш» → введите сочетание ниже.")),
            SettingsCard.Group("Горячие клавиши",
                Card(Glyphs.Keyboard, "Шторка уведомлений", null, Hotkey(s.NotificationsHotkey, v => Up(x => x with { NotificationsHotkey = v }))),
                Card(Glyphs.Keyboard, "Быстрые настройки", null, Hotkey(s.QuickSettingsHotkey, v => Up(x => x with { QuickSettingsHotkey = v }))),
                Card(Glyphs.Keyboard, "Обе шторки сразу", null, Hotkey(s.ToggleBothHotkey, v => Up(x => x with { ToggleBothHotkey = v })))),
            SettingsCard.Group("Внешний вид",
                Card(Glyphs.Shade, "Ширина шторки", "В единицах интерфейса", Ui.Ui.Slider(320, 560, s.Width, v => Up(x => x with { Width = v }), 10)),
                Card(Glyphs.Settings, "Плиток в ряд", null, Ui.Ui.Number(s.TileColumns, 2, 6, v => Up(x => x with { TileColumns = (int)v }))),
                Toggle(Glyphs.Brightness, "Ползунок яркости", null, s.ShowBrightness, v => Up(x => x with { ShowBrightness = v })),
                Toggle(Glyphs.Volume, "Ползунок громкости", null, s.ShowVolume, v => Up(x => x with { ShowVolume = v })),
                Toggle(Glyphs.Music, "Громкость по приложениям", null, s.ShowPerAppVolume, v => Up(x => x with { ShowPerAppVolume = v })),
                Toggle(Glyphs.Play, "Карточка «Сейчас играет»", null, s.ShowMediaCard, v => Up(x => x with { ShowMediaCard = v })),
                Toggle(Glyphs.Sparkle, "Островок у верхнего края", "Короткие подсказки: трек, смена палитры, пипетка", s.Island, v => Up(x => x with { Island = v }))),
            SettingsCard.Group("Плитки быстрых настроек", tiles),
            Ui.Ui.HStack(8,
                Ui.Ui.Button("Открыть уведомления", () => App.Current.Shell.Open(ShadeKind.Notifications, ShadeTrigger.Tray), Glyphs.Bell),
                Ui.Ui.Button("Открыть быстрые настройки", () => App.Current.Shell.Open(ShadeKind.QuickSettings, ShadeTrigger.Tray), Glyphs.Settings)).With(p => p.Margin = new Thickness(0, 16, 0, 0)));
    }
}
