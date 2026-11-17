using Imagine.App.Shell;
using Imagine.App.Ui;
using Imagine.Color;
using Imagine.Core.Settings;
using Imagine.Core.Theming;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using static Imagine.App.Pages.PageHelpers;

namespace Imagine.App.Pages;

/// <summary>
/// Главная страница: обои → цвета. Никаких пресетов: всё берётся из картинки, а пользователь
/// выбирает кандидата цвета, стиль схемы, контраст и что именно перекрашивать в системе.
/// </summary>
internal sealed class WallpaperPage(AppServices services, MainWindow window)
{
    private readonly StackPanel _hero = new() { Spacing = 12 };
    private readonly StackPanel _variants = new();
    private readonly StackPanel _tones = new() { Spacing = 6 };
    private readonly TextBlock _report = Ui.Ui.Caption("");

    private ImagineSettings S => services.Current;

    public UIElement Build()
    {
        var page = Page("Обои и цвета", "Выберите обои — Imagine подберёт палитру как Material You и перекрасит Windows, шторки, часы, терминал и редактор.",
            _hero,
            SettingsCard.Group("Стиль палитры", _variants),
            ModeGroup(),
            SettingsCard.Group("Тональные палитры", _tones),
            ApplyGroup(),
            WallpaperGroup(),
            _report);
        RenderDynamic();
        EventHandler<ThemePalette> onPalette = (_, _) => window.DispatcherQueue.Post(RenderDynamic);
        Palette.Changed += onPalette;
        page.Unloaded += (_, _) => Palette.Changed -= onPalette;
        EventHandler<Core.Services.ApplyReport> onApplied = (_, r) => ShowReport(r);
        services.Applied += onApplied;
        page.Unloaded += (_, _) => services.Applied -= onApplied;
        if (services.LastReport is { } last) ShowReport(last);
        return page;
    }

    private void ShowReport(Core.Services.ApplyReport r)
    {
        _report.Text = $"Последнее применение: {string.Join(", ", r.Applied)}" + (r.Failed.Count > 0 ? $". Не получилось: {string.Join("; ", r.Failed)}" : "");
    }

    private void RenderDynamic()
    {
        RenderHero();
        RenderVariants();
        RenderTones();
    }

    private void RenderHero()
    {
        _hero.Children.Clear();
        var path = S.Wallpaper.Path ?? services.Wallpapers.GetWallpaper();
        var preview = new Border
        {
            Height = 230,
            CornerRadius = new CornerRadius(16),
            Background = Palette.Get(ColorRole.SurfaceContainerHigh),
            BorderBrush = Ui.Ui.ThemeBrush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
        };
        if (path is not null && File.Exists(path))
            preview.Child = new Image { Source = Ui.Ui.Thumbnail(path, 800), Stretch = Stretch.UniformToFill };
        else
            preview.Child = Ui.Ui.Icon(Glyphs.Picture, 48).With(i => { i.HorizontalAlignment = HorizontalAlignment.Center; i.VerticalAlignment = VerticalAlignment.Center; i.Opacity = 0.4; });

        var info = new StackPanel { Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        var analysis = services.Engine.CurrentAnalysis;
        var theme = Palette.Current;
        info.Children.Add(Ui.Ui.Text("Цвет с обоев", 13, 600, opacity: 0.8));
        var seeds = Ui.Ui.HStack(10);
        if (analysis is not null)
        {
            for (int i = 0; i < analysis.SeedCandidates.Count; i++)
            {
                int index = i;
                bool selected = S.Theme.SeedSource == SeedSource.Wallpaper && S.Theme.WallpaperSeedIndex == i;
                var b = new Button { Content = Swatches.Dot(analysis.SeedCandidates[i], 44, selected), Padding = new Thickness(0), CornerRadius = new CornerRadius(22), Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0) };
                ToolTipService.SetToolTip(b, $"{ColorUtils.ToHex(analysis.SeedCandidates[i])}" + (i == 0 ? " — лучший кандидат" : ""));
                b.Click += (_, _) => services.ThemeSettingsChanged(s => s with { Theme = s.Theme with { SeedSource = SeedSource.Wallpaper, WallpaperSeedIndex = index } });
                seeds.Children.Add(b);
            }
        }
        if (S.Theme.CustomSeed is { } custom)
        {
            var b = new Button { Content = Swatches.Dot(custom, 44, S.Theme.SeedSource == SeedSource.Custom), Padding = new Thickness(0), CornerRadius = new CornerRadius(22), Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0) };
            ToolTipService.SetToolTip(b, "Свой цвет " + ColorUtils.ToHex(custom));
            b.Click += (_, _) => services.ThemeSettingsChanged(s => s with { Theme = s.Theme with { SeedSource = SeedSource.Custom } });
            seeds.Children.Add(b);
        }
        info.Children.Add(seeds);
        if (theme is not null)
        {
            info.Children.Add(Ui.Ui.Text($"{ColorUtils.ToHex(theme.Seed)} · {QuickTileCatalog.VariantName(theme.Variant)} · {(theme.IsDark ? "тёмная" : "светлая")}", 14, 500));
            var accents = Ui.Ui.HStack(0);
            foreach (var c in theme.Accent.All.Take(7))
                accents.Children.Add(new Border { Width = 34, Height = 22, Background = Palette.Brush(c) });
            info.Children.Add(new Border { CornerRadius = new CornerRadius(8), Child = accents, HorizontalAlignment = HorizontalAlignment.Left });
            info.Children.Add(Ui.Ui.Caption("Акцентная палитра Windows (7 оттенков)"));
        }
        if (analysis is not null)
            info.Children.Add(Ui.Ui.Caption($"Яркость обоев {analysis.AverageTone:0}, красочность {analysis.Colorfulness:0.0}" + (analysis.IsPortrait ? " · вертикальная картинка" : "")));

        var pick = Ui.Ui.Button("Выбрать обои", async () =>
        {
            var file = await PickImageAsync(window.Handle);
            if (file is not null) await services.SetWallpaperAsync(file);
        }, Glyphs.Picture, accent: true);
        var apply = Ui.Ui.Button("Применить сейчас", async () => await services.ApplyAsync("Кнопка «Применить»"), Glyphs.Sparkle);
        info.Children.Add(Ui.Ui.HStack(8, pick, apply));

        var grid = Ui.Ui.Grid("1.1*,*", preview, info);
        grid.ColumnSpacing = 24;
        _hero.Children.Add(grid);
    }

    private void RenderVariants()
    {
        _variants.Children.Clear();
        var theme = Palette.Current;
        if (theme is null) return;
        var items = new List<UIElement>();
        foreach (var (variant, scheme) in ThemeEngine.PreviewVariants(theme.Seed, theme.IsDark))
        {
            var v = variant;
            bool selected = S.Theme.Variant == v;
            var content = Ui.Ui.VStack(6,
                Swatches.SchemeBadge(scheme, 56, selected).With(g => g.HorizontalAlignment = HorizontalAlignment.Center),
                Ui.Ui.Text(QuickTileCatalog.VariantName(v), 12, selected ? 600 : 400).With(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.TextAlignment = TextAlignment.Center; }));
            var b = new Button { Content = content, HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(6, 10, 6, 8), Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0) };
            b.Click += (_, _) => services.ThemeSettingsChanged(s => s with { Theme = s.Theme with { Variant = v } });
            items.Add(b);
        }
        _variants.Children.Add(new Border
        {
            Padding = new Thickness(8),
            CornerRadius = new CornerRadius(8),
            Background = Ui.Ui.ThemeBrush("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = Ui.Ui.ThemeBrush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            Child = Uniform(5, 4, items),
        });
    }

    private void RenderTones()
    {
        _tones.Children.Clear();
        if (Palette.Current is not { } theme) return;
        var sc = theme.Scheme;
        foreach (var (p, name) in new[] { (sc.PrimaryPalette, "Основной"), (sc.SecondaryPalette, "Вторичный"), (sc.TertiaryPalette, "Третичный"), (sc.NeutralPalette, "Нейтральный"), (sc.NeutralVariantPalette, "Нейтральный вариант"), (sc.ErrorPalette, "Ошибка") })
            _tones.Children.Add(Ui.Ui.Grid("150,*", Ui.Ui.Caption(name).With(t => t.VerticalAlignment = VerticalAlignment.Center), Swatches.ToneStrip(p, name)));
    }

    private UIElement ModeGroup()
    {
        var mode = Ui.Ui.Combo(new[]
        {
            (ThemeModePreference.FromWallpaper, "По яркости обоев"),
            (ThemeModePreference.BySchedule, "По расписанию"),
            (ThemeModePreference.Light, "Всегда светлая"),
            (ThemeModePreference.Dark, "Всегда тёмная"),
        }, S.Theme.Mode, v => services.ThemeSettingsChanged(s => s with { Theme = s.Theme with { Mode = v } }));

        var contrast = Ui.Ui.Combo(new[] { (-1.0, "Пониженный"), (0.0, "Обычный"), (0.5, "Повышенный"), (1.0, "Максимальный") },
            new[] { -1.0, 0.0, 0.5, 1.0 }.MinBy(x => Math.Abs(x - S.Theme.ContrastLevel)),
            v => services.ThemeSettingsChanged(s => s with { Theme = s.Theme with { ContrastLevel = v } }));

        var source = Ui.Ui.Combo(new[]
        {
            (SeedSource.Wallpaper, "Обои"),
            (SeedSource.Custom, "Свой цвет"),
            (SeedSource.NowPlaying, "Обложка трека"),
        }, S.Theme.SeedSource, v => services.ThemeSettingsChanged(s => s with { Theme = s.Theme with { SeedSource = v } }));

        var picker = new ColorPicker
        {
            Color = Palette.ToColor(S.Theme.CustomSeed ?? Palette.Current?.Seed ?? 0xFFD55E5D),
            IsAlphaEnabled = false,
            IsMoreButtonVisible = true,
            ColorSpectrumShape = ColorSpectrumShape.Ring,
        };
        var useColor = Ui.Ui.Button("Использовать этот цвет", () =>
        {
            var c = picker.Color;
            uint argb = 0xFF000000u | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
            services.ThemeSettingsChanged(s => s with { Theme = s.Theme with { SeedSource = SeedSource.Custom, CustomSeed = argb } });
        }, Glyphs.ColorPicker, accent: true);
        var eyedropper = Ui.Ui.Button("Взять с экрана", () => App.Current.Shell.BeginColorPick(), Glyphs.ColorPicker);
        var custom = new Expander
        {
            Header = Ui.Ui.HStack(12, Ui.Ui.Icon(Glyphs.ColorPicker, 18), Ui.Ui.VStack(0, Ui.Ui.Text("Свой цвет"), Ui.Ui.Caption("Если цвет с обоев не нравится — задайте затравку сами или возьмите пипеткой"))),
            Content = Ui.Ui.VStack(12, picker, Ui.Ui.HStack(8, useColor, eyedropper)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };

        return SettingsCard.Group("Режим и источник цвета",
            Card(Glyphs.DarkMode, "Светлая или тёмная", "«По яркости обоев» — как на телефоне: светлые обои дают светлую тему", mode),
            Card(Glyphs.Light, "Контраст", "Повышенный удобнее на солнце и для слабого зрения", contrast),
            Card(Glyphs.Palette, "Источник цвета", "«Обложка трека» перекрашивает систему под играющую музыку и возвращает цвета обоев после паузы", source),
            custom);
    }

    private UIElement ApplyGroup()
    {
        var a = S.Apply;
        void Up(Func<ApplySettings, ApplySettings> f) => services.ThemeSettingsChanged(s => s with { Apply = f(s.Apply) });
        return SettingsCard.Group("Что перекрашивать",
            Toggle(Glyphs.Palette, "Акцентный цвет Windows", "Кнопки, ссылки, выделение во всех приложениях", a.WindowsAccent, v => Up(x => x with { WindowsAccent = v })),
            Toggle(Glyphs.Shade, "Акцент на панели задач и в «Пуске»", "Работает в тёмном режиме Windows", a.AccentOnTaskbarAndStart, v => Up(x => x with { AccentOnTaskbarAndStart = v })),
            Toggle(Glyphs.Shade, "Акцент в заголовках и рамках окон", null, a.AccentOnTitleBars, v => Up(x => x with { AccentOnTitleBars = v })),
            Toggle(Glyphs.DarkMode, "Режим Windows вслед за палитрой", "Светлая/тёмная тема приложений и системы", a.WindowsThemeMode, v => Up(x => x with { WindowsThemeMode = v })),
            Toggle(Glyphs.Glass, "Прозрачность Windows", null, a.Transparency, v => Up(x => x with { Transparency = v })),
            Toggle(Glyphs.Light, "Подсветка клавиатуры и мыши", "Dynamic Lighting (Windows 11 23H2+): устройства светятся цветами палитры", a.DynamicLighting, v => Up(x => x with { DynamicLighting = v })),
            Toggle(Glyphs.Sparkle, "«Дыхание» палитры", "Вечером цвета чуть теплеют, ночью успокаиваются", a.BreathingPalette, v => Up(x => x with { BreathingPalette = v })),
            Toggle(Glyphs.Music, "Цвет обложки трека", "Пока играет музыка, акцент берётся с обложки", a.NowPlayingTint, v => Up(x => x with { NowPlayingTint = v })));
    }

    private UIElement WallpaperGroup()
    {
        var w = S.Wallpaper;
        void Up(Func<WallpaperSettings, WallpaperSettings> f) => services.UpdateSettings(s => s with { Wallpaper = f(s.Wallpaper) });
        var fit = Ui.Ui.Combo(new[]
        {
            (WallpaperFit.Fill, "Заполнение"), (WallpaperFit.Fit, "По размеру"), (WallpaperFit.Stretch, "Растянуть"),
            (WallpaperFit.Tile, "Замостить"), (WallpaperFit.Center, "По центру"), (WallpaperFit.Span, "Расширение"),
        }, w.Fit, v =>
        {
            Up(x => x with { Fit = v });
            if (S.Wallpaper.Path is { } p) services.Wallpapers.SetWallpaper(p, null, v);
        });

        var folderText = Ui.Ui.Caption(w.RotationFolder ?? "Папка не выбрана");
        var pickFolder = Ui.Ui.Button("Выбрать папку", async () =>
        {
            var f = await PickFolderAsync(window.Handle);
            if (f is null) return;
            Up(x => x with { RotationFolder = f });
            folderText.Text = f;
        }, Glyphs.Folder);
        var interval = Ui.Ui.Combo(new[]
        {
            (0, "Не менять"), (15, "Каждые 15 минут"), (60, "Каждый час"), (180, "Каждые 3 часа"), (1440, "Раз в день"),
        }, (int)(w.RotationInterval?.TotalMinutes ?? 0), v => Up(x => x with { RotationInterval = v == 0 ? null : TimeSpan.FromMinutes(v) }));

        bool lockSupported = services.LockScreen.IsSupported;
        return SettingsCard.Group("Обои",
            Card(Glyphs.Picture, "Размещение", null, fit),
            Toggle(Glyphs.Lock, "Те же обои на экране блокировки", lockSupported ? null : "Доступно в версии MSIX: у обычной сборки Windows не даёт менять экран блокировки", w.SyncLockScreen, v => Up(x => x with { SyncLockScreen = v })),
            Toggle(Glyphs.Clock, "Часы Imagine на экране блокировки", "Рисуются прямо на картинке; системные часы можно скрыть в параметрах экрана блокировки", w.BakeClockIntoLockScreen, v =>
            {
                Up(x => x with { BakeClockIntoLockScreen = v });
                if (v) _ = services.BakeLockScreenAsync();
            }),
            Toggle(Glyphs.Rotate, "Поворачивать вертикальные фото", "Фото с телефона, снятое боком, ляжет на экран правильно", w.AutoRotate, v => Up(x => x with { AutoRotate = v })),
            Card(Glyphs.Shuffle, "Смена обоев из папки", "Палитра перестраивается под каждую новую картинку", interval, Ui.Ui.HStack(12, pickFolder, folderText.With(t => t.VerticalAlignment = VerticalAlignment.Center))));
    }
}
