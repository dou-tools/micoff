namespace MicOff;

// mismos valores que MOD_* de RegisterHotKey
[Flags]
enum KeyModifiers
{
    None = 0,
    Alt = 1,
    Ctrl = 2,
    Shift = 4,
    Win = 8,
}

readonly record struct Hotkey(Keys Key, KeyModifiers Modifiers)
{
    public bool IsMouse => Key is Keys.XButton1 or Keys.XButton2;

    public static bool TryParse(string text, out Hotkey hotkey)
    {
        hotkey = default;
        var modifiers = KeyModifiers.None;
        Keys? key = null;
        foreach (var part in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (Enum.TryParse<KeyModifiers>(part, true, out var modifier) && modifier != KeyModifiers.None)
                modifiers |= modifier;
            else if (Enum.TryParse<Keys>(part, true, out var parsed))
                key = parsed;
            else
                return false;
        }
        if (key == null) return false;
        hotkey = new Hotkey(key.Value, modifiers);
        return true;
    }

    public override string ToString()
    {
        var modifiers = Modifiers;
        var parts = new[] { KeyModifiers.Ctrl, KeyModifiers.Alt, KeyModifiers.Shift, KeyModifiers.Win }
            .Where(m => modifiers.HasFlag(m))
            .Select(m => m.ToString())
            .ToList();
        parts.Add(Key.ToString());
        return string.Join("+", parts);
    }
}
