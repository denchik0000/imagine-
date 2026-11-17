using System.Collections.Frozen;
using Imagine.Color;

namespace Imagine.Core.Theming;

/// <summary>Готовая тема: схема, акцент Windows и токены стекла.</summary>
public sealed class ThemePalette
{
    public required uint Seed { get; init; }
    public required Variant Variant { get; init; }
    public required bool IsDark { get; init; }
    public required double ContrastLevel { get; init; }
    public required DynamicScheme Scheme { get; init; }
    /// <summary>Пара схемы с противоположной яркостью (для превью и тем приложений с обоими режимами).</summary>
    public required DynamicScheme OppositeScheme { get; init; }
    public required FrozenDictionary<ColorRole, uint> Roles { get; init; }
    public required WindowsAccentPalette Accent { get; init; }
    public required GlassTokens Glass { get; init; }

    public uint this[ColorRole role] => Roles[role];

    public DynamicScheme LightScheme => IsDark ? OppositeScheme : Scheme;
    public DynamicScheme DarkScheme => IsDark ? Scheme : OppositeScheme;

    public override string ToString() => $"{ColorUtils.ToHex(Seed)} {Variant} {(IsDark ? "тёмная" : "светлая")}";
}
