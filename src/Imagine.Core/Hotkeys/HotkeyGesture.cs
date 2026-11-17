using System.Globalization;
using System.Text;

namespace Imagine.Core.Hotkeys;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>Сочетание клавиш, например «Win+Alt+N». Коды клавиш — виртуальные коды Windows (VK_*).</summary>
public readonly record struct HotkeyGesture(HotkeyModifiers Modifiers, int VirtualKey)
{
    private static readonly Dictionary<string, int> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Space"] = 0x20, ["Enter"] = 0x0D, ["Tab"] = 0x09, ["Esc"] = 0x1B, ["Escape"] = 0x1B,
        ["Up"] = 0x26, ["Down"] = 0x28, ["Left"] = 0x25, ["Right"] = 0x27,
        ["Home"] = 0x24, ["End"] = 0x23, ["PageUp"] = 0x21, ["PageDown"] = 0x22,
        ["Insert"] = 0x2D, ["Delete"] = 0x2E, ["Backspace"] = 0x08,
        ["`"] = 0xC0, ["-"] = 0xBD, ["="] = 0xBB, ["["] = 0xDB, ["]"] = 0xDD, [";"] = 0xBA, ["'"] = 0xDE, [","] = 0xBC, ["."] = 0xBE, ["/"] = 0xBF, ["\\"] = 0xDC,
    };

    public bool IsEmpty => VirtualKey == 0;

    public static HotkeyGesture Parse(string text)
    {
        if (!TryParse(text, out var g)) throw new FormatException($"Не удалось разобрать сочетание «{text}».");
        return g;
    }

    public static bool TryParse(string? text, out HotkeyGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (text.EndsWith("++", StringComparison.Ordinal)) parts = [.. parts, "="]; // «Ctrl++» трактуем как «Ctrl+=»
        var mods = HotkeyModifiers.None;
        int key = 0;
        foreach (var p in parts)
        {
            switch (p.ToUpperInvariant())
            {
                case "WIN": case "WINDOWS": case "META": mods |= HotkeyModifiers.Win; continue;
                case "CTRL": case "CONTROL": mods |= HotkeyModifiers.Control; continue;
                case "ALT": mods |= HotkeyModifiers.Alt; continue;
                case "SHIFT": mods |= HotkeyModifiers.Shift; continue;
            }
            if (key != 0) return false;
            if (p.Length == 1 && char.IsLetterOrDigit(p[0]) && p[0] < 128) key = char.ToUpperInvariant(p[0]);
            else if (p.Length >= 2 && (p[0] == 'F' || p[0] == 'f') && int.TryParse(p[1..], NumberStyles.None, CultureInfo.InvariantCulture, out int f) && f is >= 1 and <= 24) key = 0x70 + f - 1;
            else if (Named.TryGetValue(p, out int vk)) key = vk;
            else return false;
        }
        if (key == 0) return false;
        gesture = new HotkeyGesture(mods, key);
        return true;
    }

    public override string ToString()
    {
        if (IsEmpty) return "";
        var sb = new StringBuilder();
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) sb.Append("Win+");
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) sb.Append("Ctrl+");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) sb.Append("Alt+");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) sb.Append("Shift+");
        sb.Append(KeyName(VirtualKey));
        return sb.ToString();
    }

    private static string KeyName(int vk)
    {
        if (vk is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A) return ((char)vk).ToString();
        if (vk is >= 0x70 and <= 0x87) return "F" + (vk - 0x70 + 1).ToString(CultureInfo.InvariantCulture);
        foreach (var (name, code) in Named) if (code == vk) return name;
        return "0x" + vk.ToString("X2", CultureInfo.InvariantCulture);
    }
}
