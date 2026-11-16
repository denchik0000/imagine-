using Imagine.App.Shell;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.XamlTypeInfo;

namespace Imagine.App;

/// <summary>
/// Приложение без XAML-разметки. Метаданные стандартных элементов отдаёт XamlControlsXamlMetaDataProvider,
/// поэтому NavigationView, NumberBox и прочие элементы WinUI работают так же, как из XAML.
/// </summary>
public sealed partial class App : Application, IXamlMetadataProvider
{
    private readonly XamlControlsXamlMetaDataProvider _provider = new();
    private readonly string[] _args;
    private MainWindow? _main;

    public App(string[] args)
    {
        _args = args;
        UnhandledException += (_, e) =>
        {
            Log.Error("Необработанное исключение", e.Exception);
            e.Handled = true;
        };
        // Исключения из фоновых потоков и забытых задач тоже пишем в журнал, чтобы было что разбирать.
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error("Фатальное исключение", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error("Незамеченное исключение задачи", e.Exception);
            e.SetObserved();
        };
    }

    public static new App Current => (App)Application.Current;

    public AppServices Services { get; private set; } = null!;

    public ShellController Shell { get; private set; } = null!;

    public Window? MainWindowInstance => _main;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Resources.MergedDictionaries.Add(new XamlControlsResources());
        Services = new AppServices();
        Shell = new ShellController(Services);
        Shell.Start();
        _ = Services.StartAsync();

        bool silent = _args.Contains("--background", StringComparer.OrdinalIgnoreCase);
        if (!silent) ShowSettings();
    }

    public void OnRedirectedActivation() => Services.Dispatcher.TryEnqueue(() => ShowSettings());

    public void ShowSettings(string? page = null)
    {
        if (_main is null)
        {
            _main = new MainWindow(Services);
            _main.Closed += (_, _) => _main = null;
        }
        if (page is not null) _main.Navigate(page);
        _main.Activate();
        Platform.Windowing.WindowStyler.BringToFront(WinRT.Interop.WindowNative.GetWindowHandle(_main), true);
    }

    public void Quit()
    {
        Shell.Dispose();
        Services.Dispose();
        _main?.Close();
        Exit();
    }

    public IXamlType GetXamlType(Type type) => _provider.GetXamlType(type);

    public IXamlType GetXamlType(string fullName) => _provider.GetXamlType(fullName);

    public XmlnsDefinition[] GetXmlnsDefinitions() => _provider.GetXmlnsDefinitions();
}

/// <summary>Простой журнал в %LocalAppData%\Imagine\imagine.log.</summary>
public static class Log
{
    private static readonly object Gate = new();
    private static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Imagine", "imagine.log");

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? ex = null) => Write("FAIL", ex is null ? message : $"{message}: {ex}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var info = new FileInfo(FilePath);
                if (info.Exists && info.Length > 2_000_000) File.Move(FilePath, FilePath + ".old", true);
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}{Environment.NewLine}");
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
