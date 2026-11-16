using Imagine.App.Pages;
using Imagine.App.Ui;
using Imagine.Platform.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Imagine.App;

/// <summary>Окно настроек: навигация слева, страницы строятся кодом.</summary>
public sealed class MainWindow : Window
{
    private readonly AppServices _services;
    private readonly NavigationView _nav;
    private readonly ContentControl _content = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };

    private static readonly (string Tag, string Glyph, string Title)[] PagesList =
    [
        ("wallpaper", Glyphs.Palette, "Обои и цвета"),
        ("glass", Glyphs.Glass, "Стекло и свечение"),
        ("shades", Glyphs.Shade, "Шторки"),
        ("notifications", Glyphs.Bell, "Уведомления"),
        ("clock", Glyphs.Clock, "Часы"),
        ("schedule", Glyphs.Calendar, "Расписание"),
        ("integrations", Glyphs.Link, "Интеграции и откат"),
    ];

    public MainWindow(AppServices services)
    {
        _services = services;
        Title = "Imagine";
        SystemBackdrop = new MicaBackdrop { Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt };
        ExtendsContentIntoTitleBar = true;
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Imagine.ico"));
        double scale = WindowStyler.GetScale(WinRT.Interop.WindowNative.GetWindowHandle(this));
        AppWindow.Resize(new Windows.Graphics.SizeInt32((int)(1180 * scale), (int)(820 * scale)));

        var titleBar = new Grid { Height = 48, Padding = new Thickness(16, 0, 0, 0) };
        var logo = new Image { Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "Imagine.png"))), Width = 18, Height = 18 };
        titleBar.Children.Add(Ui.Ui.HStack(10, logo, Ui.Ui.Text("Imagine", 12, 500)).With(p => p.VerticalAlignment = VerticalAlignment.Center));
        SetTitleBar(titleBar);

        _nav = new NavigationView
        {
            IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
            IsSettingsVisible = false,
            PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
            OpenPaneLength = 260,
            Content = _content,
            IsPaneToggleButtonVisible = true,
        };
        foreach (var (tag, glyph, title) in PagesList)
            _nav.MenuItems.Add(new NavigationViewItem { Content = title, Tag = tag, Icon = new FontIcon { Glyph = glyph, FontFamily = Ui.Ui.IconFont } });
        _nav.FooterMenuItems.Add(new NavigationViewItem { Content = "О программе", Tag = "about", Icon = new FontIcon { Glyph = Glyphs.Info, FontFamily = Ui.Ui.IconFont } });
        _nav.SelectionChanged += (_, e) =>
        {
            if (e.SelectedItem is NavigationViewItem { Tag: string tag }) Show(tag);
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(titleBar);
        Grid.SetRow(_nav, 1);
        root.Children.Add(_nav);
        Content = root;

        Palette.Changed += (_, t) => DispatcherQueue.TryEnqueue(() => ApplyAccent(t));
        if (Palette.Current is { } current) ApplyAccent(current);
        _nav.SelectedItem = _nav.MenuItems[0];
    }

    /// <summary>Окно настроек тоже красится палитрой: системный акцент WinUI берёт цвета схемы.</summary>
    private void ApplyAccent(Core.Theming.ThemePalette theme)
    {
        if (Content is not FrameworkElement root) return;
        var r = root.Resources;
        void Set(string key, Imagine.Color.ColorRole role) => r[key] = Palette.ToColor(theme[role]);
        Set("SystemAccentColor", Imagine.Color.ColorRole.Primary);
        r["SystemAccentColorLight1"] = Palette.ToColor(theme.Accent.Light1);
        r["SystemAccentColorLight2"] = Palette.ToColor(theme.Accent.Light2);
        r["SystemAccentColorLight3"] = Palette.ToColor(theme.Accent.Light3);
        r["SystemAccentColorDark1"] = Palette.ToColor(theme.Accent.Dark1);
        r["SystemAccentColorDark2"] = Palette.ToColor(theme.Accent.Dark2);
        r["SystemAccentColorDark3"] = Palette.ToColor(theme.Accent.Dark3);
        root.RequestedTheme = theme.IsDark ? ElementTheme.Dark : ElementTheme.Light;
    }

    public void Navigate(string tag)
    {
        foreach (var item in _nav.MenuItems.Concat(_nav.FooterMenuItems).OfType<NavigationViewItem>())
            if ((string)item.Tag == tag) _nav.SelectedItem = item;
    }

    private void Show(string tag)
    {
        try
        {
            _content.Content = tag switch
            {
                "wallpaper" => new WallpaperPage(_services, this).Build(),
                "glass" => GlassPage.Build(_services),
                "shades" => ShadesPage.Build(_services),
                "notifications" => NotificationsPage.Build(_services),
                "clock" => ClockPage.Build(_services),
                "schedule" => SchedulePage.Build(_services),
                "integrations" => IntegrationsPage.Build(_services, this),
                _ => AboutPage.Build(_services),
            };
        }
        catch (Exception ex)
        {
            Log.Error("Страница " + tag, ex);
            _content.Content = Ui.Ui.Scroll(Ui.Ui.VStack(8, Ui.Ui.Title("Не удалось открыть страницу"), Ui.Ui.Text(ex.Message)));
        }
    }

    public IntPtr Handle => WinRT.Interop.WindowNative.GetWindowHandle(this);
}
