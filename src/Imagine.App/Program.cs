using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace Imagine.App;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        // Одна копия: повторный запуск (например, из меню «Пуск») просто открывает окно настроек.
        var key = AppInstance.FindOrRegisterForKey("Imagine.Main");
        if (!key.IsCurrent)
        {
            key.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs()).AsTask().Wait();
            return 0;
        }

        Application.Start(_ =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            var app = new App(args);
            key.Activated += (_, _) => app.OnRedirectedActivation();
        });
        return 0;
    }
}
