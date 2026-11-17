using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Imagine.App.Ui;

/// <summary>Карточка настройки в стиле «Параметров» Windows 11: значок, заголовок, описание и элемент справа.</summary>
public sealed class SettingsCard : Grid
{
    public SettingsCard(string glyph, string header, string? description, UIElement? control, UIElement? below = null)
    {
        Padding = new Thickness(16, 12, 16, 12);
        CornerRadius = new CornerRadius(8);
        BorderThickness = new Thickness(1);
        Background = Ui.ThemeBrush("CardBackgroundFillColorDefaultBrush");
        BorderBrush = Ui.ThemeBrush("CardStrokeColorDefaultBrush");
        MinHeight = 64;
        ColumnSpacing = 16;
        RowSpacing = 10;
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var icon = Ui.Icon(glyph, 20);
        icon.VerticalAlignment = VerticalAlignment.Center;
        Children.Add(icon);

        var texts = Ui.VStack(2, Ui.Text(header, 14));
        if (!string.IsNullOrEmpty(description)) texts.Children.Add(Ui.Caption(description));
        texts.VerticalAlignment = VerticalAlignment.Center;
        SetColumn(texts, 1);
        Children.Add(texts);

        if (control is FrameworkElement fe)
        {
            fe.VerticalAlignment = VerticalAlignment.Center;
            SetColumn(fe, 2);
            Children.Add(fe);
        }

        if (below is FrameworkElement b)
        {
            SetRow(b, 1);
            SetColumn(b, 1);
            SetColumnSpan(b, 2);
            Children.Add(b);
        }
    }

    /// <summary>Группа карточек под общим заголовком.</summary>
    public static StackPanel Group(string title, params UIElement[] cards)
    {
        var p = Ui.VStack(4, Ui.Subtitle(title));
        foreach (var c in cards) p.Children.Add(c);
        return p;
    }
}
