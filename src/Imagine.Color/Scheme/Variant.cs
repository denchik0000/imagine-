namespace Imagine.Color;

/// <summary>Стиль цветовой схемы (как в Android 12+).</summary>
public enum Variant
{
    /// <summary>Только оттенки серого.</summary>
    Monochrome,
    /// <summary>Почти серый, едва тонированный.</summary>
    Neutral,
    /// <summary>Спокойный пастельный — схема по умолчанию в Android.</summary>
    TonalSpot,
    /// <summary>Яркий: максимум хромы в основном цвете.</summary>
    Vibrant,
    /// <summary>Выразительный: основной цвет смещён по кругу, неожиданные сочетания.</summary>
    Expressive,
    /// <summary>Точный: основной цвет совпадает с цветом обоев.</summary>
    Fidelity,
    /// <summary>Контентный: как Fidelity, третичный — аналогичный цвет.</summary>
    Content,
    /// <summary>Радуга: яркий основной, серые нейтральные.</summary>
    Rainbow,
    /// <summary>Фруктовый салат: игривый поворот оттенка.</summary>
    FruitSalad,
}
