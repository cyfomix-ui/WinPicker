namespace WinPicker;

public sealed record HotkeyDefinition(uint Modifiers, uint KeyCode, string NormalizedText);

public static class HotkeyParser
{
    public static bool TryParse(string? text, out HotkeyDefinition definition, out string error)
    {
        definition = new HotkeyDefinition(0, 0, "");
        error = "";

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Hotkey is empty.";
            return false;
        }

        var tokens = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            error = "Hotkey is empty.";
            return false;
        }

        var modifiers = NativeMethods.MOD_NOREPEAT;
        string? keyToken = null;

        foreach (var token in tokens)
        {
            if (token.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || token.Equals("Control", StringComparison.OrdinalIgnoreCase))
                modifiers |= NativeMethods.MOD_CONTROL;
            else if (token.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                modifiers |= NativeMethods.MOD_ALT;
            else if (token.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                modifiers |= NativeMethods.MOD_SHIFT;
            else if (token.Equals("Win", StringComparison.OrdinalIgnoreCase) || token.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                modifiers |= NativeMethods.MOD_WIN;
            else
                keyToken = token;
        }

        if (keyToken is null)
        {
            error = "Hotkey must include a normal key, for example Win+Alt+Space.";
            return false;
        }

        if (!TryParseKey(keyToken, out var key))
        {
            error = $"Unsupported key: {keyToken}";
            return false;
        }

        var normalized = Normalize(modifiers, key);
        definition = new HotkeyDefinition(modifiers, (uint)key, normalized);
        return true;
    }

    private static bool TryParseKey(string token, out Keys key)
    {
        key = Keys.None;

        if (token.Length == 1)
        {
            var c = char.ToUpperInvariant(token[0]);
            if (c is >= 'A' and <= 'Z')
            {
                key = (Keys)c;
                return true;
            }
            if (c is >= '0' and <= '9')
            {
                key = (Keys)c;
                return true;
            }
        }

        var aliases = new Dictionary<string, Keys>(StringComparer.OrdinalIgnoreCase)
        {
            ["Space"] = Keys.Space,
            ["Backspace"] = Keys.Back,
            ["Back"] = Keys.Back,
            ["Delete"] = Keys.Delete,
            ["Del"] = Keys.Delete,
            ["Escape"] = Keys.Escape,
            ["Esc"] = Keys.Escape,
            ["Enter"] = Keys.Enter,
            ["Return"] = Keys.Return,
            ["Tab"] = Keys.Tab,
            ["Home"] = Keys.Home,
            ["End"] = Keys.End,
            ["PageUp"] = Keys.PageUp,
            ["PageDown"] = Keys.PageDown,
            ["Up"] = Keys.Up,
            ["Down"] = Keys.Down,
            ["Left"] = Keys.Left,
            ["Right"] = Keys.Right,
            ["Insert"] = Keys.Insert,
            ["Ins"] = Keys.Insert,
        };

        if (aliases.TryGetValue(token, out key))
            return true;

        return Enum.TryParse(token, true, out key) && key != Keys.None;
    }

    private static string Normalize(uint modifiers, Keys key)
    {
        var parts = new List<string>();
        if ((modifiers & NativeMethods.MOD_WIN) != 0)
            parts.Add("Win");
        if ((modifiers & NativeMethods.MOD_CONTROL) != 0)
            parts.Add("Ctrl");
        if ((modifiers & NativeMethods.MOD_ALT) != 0)
            parts.Add("Alt");
        if ((modifiers & NativeMethods.MOD_SHIFT) != 0)
            parts.Add("Shift");

        parts.Add(KeyToText(key));
        return string.Join("+", parts);
    }

    private static string KeyToText(Keys key)
    {
        return key switch
        {
            Keys.Space => "Space",
            Keys.Back => "Backspace",
            Keys.Escape => "Esc",
            Keys.Return => "Enter",
            Keys.Delete => "Delete",
            _ => key.ToString()
        };
    }
}
