using Imagine.Platform.Windowing;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace Imagine.App.Shell;

/// <summary>Окно без рамки и заголовка, которого нет в Alt+Tab и на панели задач.</summary>
public class OverlayWindow : Window
{
    protected OverlayWindow(bool topmost, bool noActivate)
    {
        Hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = topmost;
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;
        WindowStyler.MakeOverlay(Hwnd, topmost, noActivate);
        WindowStyler.SetBorderColor(Hwnd, null);
    }

    public IntPtr Hwnd { get; }

    public bool IsVisible => AppWindow.IsVisible;

    /// <summary>Масштаб экрана (1.0 = 96 DPI).</summary>
    public double Scale => Math.Max(0.5, WindowStyler.GetScale(Hwnd));

    public void Place(int x, int y, int width, int height) => AppWindow.MoveAndResize(new RectInt32(x, y, Math.Max(1, width), Math.Max(1, height)));

    public void ShowWithoutFocus()
    {
        AppWindow.Show(false);
        WindowStyler.BringToFront(Hwnd, false);
    }

    public void Hide() => AppWindow.Hide();
}
