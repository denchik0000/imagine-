using Imagine.App.Ui;
using Imagine.Core.Settings;
using Imagine.Core.Theming;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static Imagine.App.Pages.PageHelpers;

namespace Imagine.App.Pages;

/// <summary>Время суток: смена светлой/тёмной темы и «дыхание» палитры по солнцу или по часам.</summary>
internal static class SchedulePage
{
    private static string PhaseName(DayPhase p) => p switch
    {
        DayPhase.Night => "ночь",
        DayPhase.Dawn => "рассвет",
        DayPhase.Day => "день",
        DayPhase.GoldenHour => "золотой час",
        DayPhase.Dusk => "сумерки",
        _ => p.ToString(),
    };

    public static UIElement Build(AppServices services)
    {
        var s = services.Current;
        var d = s.Schedule;
        var status = Ui.Ui.Text("", 14, 500);
        void Status()
        {
            var sc = services.Current.Schedule;
            var now = DateTimeOffset.Now;
            string text = $"Сейчас: {PhaseName(sc.PhaseAt(now))}.";
            if (sc.UseSun && SunCalculator.GetSunTimes(DateOnly.FromDateTime(now.Date), sc.Latitude, sc.Longitude) is { } sun)
                text += $" Восход {sun.Sunrise.ToLocalTime():HH:mm}, закат {sun.Sunset.ToLocalTime():HH:mm}.";
            status.Text = text;
        }
        Status();
        void Up(Func<DaySchedule, DaySchedule> f) { services.ThemeSettingsChanged(x => x with { Schedule = f(x.Schedule) }); Status(); }

        return Page("Расписание", "Палитра живёт вместе с днём: к вечеру теплеет, ночью тема становится тёмной.",
            status,
            SettingsCard.Group("Светлая и тёмная",
                Toggle(Glyphs.DarkMode, "Менять тему по времени суток", "Иначе — по яркости обоев (настройка на странице «Обои и цвета»)", s.Theme.Mode == ThemeModePreference.BySchedule,
                    v => services.ThemeSettingsChanged(x => x with { Theme = x.Theme with { Mode = v ? ThemeModePreference.BySchedule : ThemeModePreference.FromWallpaper } })),
                Toggle(Glyphs.Sun, "По восходу и закату", "Время считается по координатам, без интернета", d.UseSun, v => Up(x => x with { UseSun = v })),
                Card(Glyphs.Pin, "Координаты", "Широта и долгота (по умолчанию Москва)",
                    Ui.Ui.HStack(8, Ui.Ui.Number(d.Latitude, -90, 90, v => Up(x => x with { Latitude = v }), 0.1), Ui.Ui.Number(d.Longitude, -180, 180, v => Up(x => x with { Longitude = v }), 0.1))),
                Card(Glyphs.Sun, "Светлая с", "Если не по солнцу", Time(d.LightFrom, v => Up(x => x with { LightFrom = v }))),
                Card(Glyphs.Moon, "Тёмная с", "Если не по солнцу", Time(d.DarkFrom, v => Up(x => x with { DarkFrom = v })))),
            SettingsCard.Group("Дыхание палитры",
                Toggle(Glyphs.Sparkle, "Включено", null, s.Apply.BreathingPalette, v => services.ThemeSettingsChanged(x => x with { Apply = x.Apply with { BreathingPalette = v } })),
                Card(Glyphs.Light, "Вечернее тепло", "На сколько градусов оттенка цвета сдвигаются к тёплым в золотой час и сумерки",
                    Ui.Ui.Slider(0, 25, s.Theme.EveningWarmShift, v => services.ThemeSettingsChanged(x => x with { Theme = x.Theme with { EveningWarmShift = v } }))),
                Card(Glyphs.Music, "Сила цвета обложки", "Насколько цвет играющего трека перебивает цвет обоев",
                    Ui.Ui.Slider(0, 1, s.Theme.NowPlayingBlend, v => services.ThemeSettingsChanged(x => x with { Theme = x.Theme with { NowPlayingBlend = v } }), 0.05))));
    }
}
