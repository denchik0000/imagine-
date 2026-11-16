using Imagine.Color;

namespace Imagine.Core.Clock;

/// <summary>Раскладка цифр.</summary>
public enum ClockLayout
{
    /// <summary>Часы над минутами крупными цифрами (фирменный вид ColorOS).</summary>
    Stacked,
    /// <summary>В одну строку.</summary>
    Inline,
    /// <summary>Тонкие цифры с датой слева, как журнальная вёрстка.</summary>
    Editorial,
    /// <summary>Стрелочные с отметками в цвет палитры.</summary>
    Analog,
}

public enum ClockAlignment { Left, Center, Right }

/// <summary>Где живут часы на рабочем столе.</summary>
public enum ClockPlacement { TopLeft, TopCenter, TopRight, Center, BottomLeft, BottomRight, Custom }

/// <summary>Стиль часов: используется и для виджета, и для картинки экрана блокировки, чтобы переход выглядел «переездом».</summary>
public sealed record ClockStyle
{
    public ClockLayout Layout { get; init; } = ClockLayout.Stacked;
    public ClockAlignment Alignment { get; init; } = ClockAlignment.Left;
    public ClockPlacement Placement { get; init; } = ClockPlacement.TopLeft;
    /// <summary>Шрифт (вариативные шрифты Windows 11: Segoe UI Variable Display, Bahnschrift).</summary>
    public string FontFamily { get; init; } = "Segoe UI Variable Display";
    /// <summary>Толщина 100…900.</summary>
    public int FontWeight { get; init; } = 300;
    /// <summary>Высота цифр относительно высоты экрана (0.05…0.4).</summary>
    public double RelativeSize { get; init; } = 0.16;
    /// <summary>Роль цвета из палитры; null — автоматически по яркости обоев.</summary>
    public ColorRole? ColorRole { get; init; }
    public bool Use24Hour { get; init; } = true;
    public bool ShowSeconds { get; init; }
    public bool ShowDate { get; init; } = true;
    public bool ShowWeather { get; init; }
    /// <summary>Мягкая тень/свечение под цифрами для читаемости на пёстрых обоях.</summary>
    public bool Glow { get; init; } = true;
    /// <summary>Пользовательская позиция (доли экрана), если <see cref="Placement"/> = Custom.</summary>
    public double CustomX { get; init; } = 0.06;
    public double CustomY { get; init; } = 0.08;
}
