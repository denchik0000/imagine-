namespace Imagine.Core.Shades;

/// <summary>Какая шторка.</summary>
public enum ShadeKind
{
    /// <summary>Левая: уведомления и состояния.</summary>
    Notifications,
    /// <summary>Правая: быстрые настройки и медиа.</summary>
    QuickSettings,
}

/// <summary>Чем открыли шторку.</summary>
public enum ShadeTrigger
{
    MouseEdgeDwell,
    MouseEdgePush,
    /// <summary>Двумя пальцами вниз по тачпаду (прокрутка) у верхнего края.</summary>
    TouchpadPull,
    /// <summary>Свайп тремя или четырьмя пальцами, назначенный в Windows на нашу горячую клавишу.</summary>
    TouchpadGesture,
    Hotkey,
    Tray,
    Touch,
}

public sealed record ShadeRequest(ShadeKind Kind, ShadeTrigger Trigger, double AnchorX);
