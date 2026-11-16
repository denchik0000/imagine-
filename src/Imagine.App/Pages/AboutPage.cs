using Imagine.App.Ui;
using Imagine.Platform.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using static Imagine.App.Pages.PageHelpers;

namespace Imagine.App.Pages;

internal static class AboutPage
{
    public static UIElement Build(AppServices services)
    {
        var g = services.Current.General;
        bool packaged = services.LockScreen.IsSupported;
        var version = typeof(AboutPage).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
        var logo = new Image { Source = new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "Imagine.png"))), Width = 72, Height = 72 };
        var hero = Ui.Ui.HStack(18, logo, Ui.Ui.VStack(2,
            Ui.Ui.Text("Imagine", 28, 600),
            Ui.Ui.Caption($"Версия {version} · {(packaged ? "MSIX-пакет" : "обычная сборка")}"),
            Ui.Ui.Text("Палитра из обоев, матовое стекло, шторки и часы в духе ColorOS — для Windows 11.", 14)));

        return Page("О программе", "Без прав администратора: всё, что меняет Imagine, — это ваши пользовательские настройки, и их можно откатить.",
            hero,
            SettingsCard.Group("Запуск",
                Toggle(Glyphs.Pin, "Запускать вместе с Windows", null, g.StartWithWindows, v =>
                {
                    services.UpdateSettings(x => x with { General = x.General with { StartWithWindows = v } });
                    StartupService.SetEnabled(v, Environment.ProcessPath ?? "");
                }),
                Toggle(Glyphs.Pin, "Значок в области уведомлений", "Левый клик — быстрые настройки, правый — меню", g.ShowTrayIcon, v => services.UpdateSettings(x => x with { General = x.General with { ShowTrayIcon = v } }))),
            SettingsCard.Group("Файлы",
                Card(Glyphs.Settings, "Файл настроек", services.Settings.Path, Ui.Ui.Button("Показать", () => SystemActions.Open(Path.GetDirectoryName(services.Settings.Path)!), Glyphs.Folder)),
                Card(Glyphs.Info, "Журнал", "Если что-то не работает — пришлите его", Ui.Ui.Button("Открыть", () => SystemActions.Open(Path.Combine(services.DataDirectory, "imagine.log")), Glyphs.Folder))),
            SettingsCard.Group("Что зависит от версии",
                Info(packaged ? "Установлена версия MSIX" : "Обычная сборка (без установки)",
                    packaged
                        ? "Доступно всё: чтение уведомлений в шторке и смена картинки экрана блокировки."
                        : "Шторка уведомлений не может читать уведомления Windows, и картинку экрана блокировки поменять нельзя — Windows разрешает это только упакованным приложениям. Всё остальное работает.",
                    packaged ? InfoBarSeverity.Success : InfoBarSeverity.Informational),
                Info("Ограничения Windows", "Кнопки внутри чужих уведомлений нажать нельзя. Системные шторки Windows (Win+N, Win+A) не заменяются, Imagine открывает свои рядом. Жесты тремя-четырьмя пальцами назначаются на горячие клавиши в параметрах сенсорной панели.")));
    }
}
