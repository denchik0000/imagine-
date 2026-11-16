using Imagine.App.Ui;
using Imagine.Color;
using Imagine.Core.Theming;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using static Imagine.App.Pages.PageHelpers;

namespace Imagine.App.Pages;

/// <summary>Матовое стекло и Contour Glow: плотность, свечение, скругления — с живым превью на ваших обоях.</summary>
internal static class GlassPage
{
    public static UIElement Build(AppServices services)
    {
        var g = services.Current.Glass;
        var glow = BuildPreview(services, out var previewGlow);

        void Up(Func<GlassStyle, GlassStyle> f)
        {
            services.ThemeSettingsChanged(s => s with { Glass = f(s.Glass) });
            var n = services.Current.Glass;
            previewGlow.Configure(n.GlowMode, n.GlowIntensity, n.GlowThickness, n.CornerRadius);
        }

        var mode = Ui.Ui.Radios(new[]
        {
            (GlowMode.Off, "Без свечения"),
            (GlowMode.Accent, "Акцент — однотонная обводка"),
            (GlowMode.Duotone, "Дуотон — акцент и третичный"),
            (GlowMode.Aurora, "Аврора — медленно переливается"),
        }, g.GlowMode, v => Up(x => x with { GlowMode = v }));

        return Page("Стекло и свечение", "Как в ColorOS 17: матовое стекло с оттенком палитры и светящийся контур у шторок и островка.",
            glow,
            SettingsCard.Group("Матовое стекло",
                Card(Glyphs.Glass, "Матовость", "Слева — почти прозрачное, справа — плотное молочное стекло", Ui.Ui.Slider(0, 1, g.Frost, v => Up(x => x with { Frost = v }), 0.05)),
                Card(Glyphs.Crop, "Скругление углов", null, Ui.Ui.Slider(0, 40, g.CornerRadius, v => Up(x => x with { CornerRadius = v })))),
            SettingsCard.Group("Contour Glow",
                Card(Glyphs.Sparkle, "Вид свечения", null, null, mode),
                Card(Glyphs.Light, "Яркость", null, Ui.Ui.Slider(0, 1, g.GlowIntensity, v => Up(x => x with { GlowIntensity = v }), 0.05)),
                Card(Glyphs.Shade, "Толщина контура", null, Ui.Ui.Slider(0.5, 4, g.GlowThickness, v => Up(x => x with { GlowThickness = v }), 0.25))));
    }

    private static UIElement BuildPreview(AppServices services, out ContourGlow glow)
    {
        var path = services.Current.Wallpaper.Path ?? services.Wallpapers.GetWallpaper();
        var stage = new Grid { Height = 260, CornerRadius = new CornerRadius(16) };
        if (path is not null && File.Exists(path))
            stage.Children.Add(new Image { Source = Ui.Ui.Thumbnail(path, 1000), Stretch = Stretch.UniformToFill });
        else stage.Background = Palette.Get(ColorRole.PrimaryContainer);

        // Превью стекла внутри окна: настоящий размытый фон рабочего стола даёт только сама шторка,
        // здесь — полупрозрачная поверхность с тинтом той же палитры.
        var glass = new Border
        {
            Background = Palette.Get(ColorRole.SurfaceContainer, 0.62),
            CornerRadius = new CornerRadius(services.Current.Glass.CornerRadius),
            Padding = new Thickness(18),
            Child = Ui.Ui.VStack(8,
                Ui.Ui.Text("12:30", 40, 300, Palette.Get(ColorRole.Primary)),
                Ui.Ui.Text("Так будут выглядеть шторки", 14, 500, Palette.Get(ColorRole.OnSurface)),
                Ui.Ui.HStack(8,
                    new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(22), Background = Palette.Get(ColorRole.Primary), Child = Ui.Ui.Icon(Glyphs.Wifi, 16, Palette.Get(ColorRole.OnPrimary)).With(i => { i.HorizontalAlignment = HorizontalAlignment.Center; }) },
                    new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(22), Background = Palette.Get(ColorRole.SurfaceContainerHighest, 0.7), Child = Ui.Ui.Icon(Glyphs.Bluetooth, 16, Palette.Get(ColorRole.OnSurface)).With(i => { i.HorizontalAlignment = HorizontalAlignment.Center; }) },
                    new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(22), Background = Palette.Get(ColorRole.SurfaceContainerHighest, 0.7), Child = Ui.Ui.Icon(Glyphs.Moon, 16, Palette.Get(ColorRole.OnSurface)).With(i => { i.HorizontalAlignment = HorizontalAlignment.Center; }) })),
        };
        glow = new ContourGlow(glass, services.Current.Glass.CornerRadius)
        {
            Width = 300,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var gs = services.Current.Glass;
        glow.Configure(gs.GlowMode, gs.GlowIntensity, gs.GlowThickness, gs.CornerRadius);
        stage.Children.Add(glow);
        return stage;
    }
}
