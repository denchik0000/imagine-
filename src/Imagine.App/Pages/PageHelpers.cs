using Imagine.App.Ui;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace Imagine.App.Pages;

internal static class PageHelpers
{
    public static ScrollViewer Page(string title, string subtitle, params UIElement[] sections)
    {
        var stack = Ui.Ui.VStack(4, Ui.Ui.Title(title), Ui.Ui.Caption(subtitle).With(t => t.Margin = new Thickness(0, 0, 0, 8)));
        foreach (var s in sections) stack.Children.Add(s);
        return Ui.Ui.Scroll(stack);
    }

    public static SettingsCard Card(string glyph, string header, string? description, UIElement? control, UIElement? below = null)
        => new(glyph, header, description, control, below);

    public static SettingsCard Toggle(string glyph, string header, string? description, bool value, Action<bool> onChanged)
        => new(glyph, header, description, Ui.Ui.Toggle(value, onChanged));

    /// <summary>Сетка с равными колонками.</summary>
    public static Grid Uniform(int columns, double spacing, IEnumerable<UIElement> items)
    {
        var g = new Grid { ColumnSpacing = spacing, RowSpacing = spacing };
        for (int c = 0; c < columns; c++) g.ColumnDefinitions.Add(new ColumnDefinition());
        int i = 0;
        foreach (var item in items)
        {
            if (i % columns == 0) g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow((FrameworkElement)item, i / columns);
            Grid.SetColumn((FrameworkElement)item, i % columns);
            g.Children.Add(item);
            i++;
        }
        return g;
    }

    public static async Task<string?> PickImageAsync(IntPtr hwnd)
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.PicturesLibrary, ViewMode = PickerViewMode.Thumbnail };
        foreach (var ext in new[] { ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".jxr", ".heic" }) picker.FileTypeFilter.Add(ext);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    public static async Task<string?> PickFolderAsync(IntPtr hwnd)
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.PicturesLibrary };
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    public static InfoBar Info(string title, string message, InfoBarSeverity severity = InfoBarSeverity.Informational) => new()
    {
        Title = title,
        Message = message,
        Severity = severity,
        IsOpen = true,
        IsClosable = false,
        Margin = new Thickness(0, 4, 0, 4),
    };

    public static TimePicker Time(TimeOnly value, Action<TimeOnly> onChanged)
    {
        var t = new TimePicker { Time = value.ToTimeSpan(), ClockIdentifier = "24HourClock", MinuteIncrement = 5 };
        t.TimeChanged += (_, e) => onChanged(TimeOnly.FromTimeSpan(e.NewTime));
        return t;
    }
}
