namespace Imagine.Core.Shades;

public enum WheelPullDirection { Positive, Negative, Any }

/// <summary>Настройки вызова шторок с верхнего края.</summary>
public sealed record EdgeSettings
{
    public bool MouseEnabled { get; init; } = true;
    /// <summary>Сколько держать курсор у края, мс (0 — только «давление»).</summary>
    public int DwellMs { get; init; } = 220;
    /// <summary>Сколько движений «в край» нужно, чтобы шторка открылась сразу, не дожидаясь задержки.</summary>
    public int PushEvents { get; init; } = 6;
    /// <summary>Толщина чувствительной полосы, px.</summary>
    public int EdgeThickness { get; init; } = 1;
    /// <summary>Где делится край: 0.5 — пополам; левая часть — уведомления, правая — настройки.</summary>
    public double SplitRatio { get; init; } = 0.5;
    /// <summary>Мёртвая зона у левого угла, px (там бывают кнопки меню приложений).</summary>
    public int LeftDeadZone { get; init; } = 24;
    /// <summary>Мёртвая зона у правого угла, px: кнопки «свернуть/развернуть/закрыть» развёрнутых окон и Snap Layouts.</summary>
    public int RightDeadZone { get; init; } = 160;
    /// <summary>Поменять местами: уведомления справа, настройки слева.</summary>
    public bool SwapSides { get; init; }
    public bool TouchpadPullEnabled { get; init; } = true;
    /// <summary>Накопленная прокрутка у края (120 = один «щелчок» колеса).</summary>
    public int WheelPullThreshold { get; init; } = 240;
    public WheelPullDirection WheelDirection { get; init; } = WheelPullDirection.Positive;
    /// <summary>Не открывать, пока зажата кнопка мыши (перетаскивание окна к краю).</summary>
    public bool IgnoreWhileButtonDown { get; init; } = true;
    /// <summary>Не открывать поверх полноэкранных приложений и игр.</summary>
    public bool DisableInFullscreen { get; init; } = true;
    /// <summary>Пауза после закрытия шторки, мс, чтобы она не открывалась снова, пока курсор у края.</summary>
    public int CooldownMs { get; init; } = 600;
}
