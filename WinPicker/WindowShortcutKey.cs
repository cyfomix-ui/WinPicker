namespace WinPicker;

internal static class WindowShortcutKey
{
    public const int MaximumAssignedWindows = 35;

    public static string? GetLabel(int index)
    {
        if (index >= 0 && index <= 8)
            return (index + 1).ToString();

        var letterIndex = index - 9;
        if (letterIndex >= 0 && letterIndex < 26)
            return ((char)('a' + letterIndex)).ToString();

        return null;
    }

    public static string? GetChordLabel(int index)
    {
        var label = GetLabel(index);
        return label is null ? null : $"Win+Alt+{label}";
    }

    public static bool TryGetIndex(Keys keyCode, out int index)
    {
        index = -1;
        var keyValue = (int)keyCode;

        if (keyValue >= (int)Keys.D1 && keyValue <= (int)Keys.D9)
        {
            index = keyValue - (int)Keys.D1;
            return true;
        }

        if (keyValue >= (int)Keys.NumPad1 && keyValue <= (int)Keys.NumPad9)
        {
            index = keyValue - (int)Keys.NumPad1;
            return true;
        }

        if (keyValue >= (int)Keys.A && keyValue <= (int)Keys.Z)
        {
            index = 9 + keyValue - (int)Keys.A;
            return true;
        }

        return false;
    }

    public static bool TryGetIndexFromVirtualKey(int key, out int index)
    {
        index = -1;
        if (key >= 0x31 && key <= 0x39)
        {
            index = key - 0x31;
            return true;
        }

        if (key >= 0x61 && key <= 0x69)
        {
            index = key - 0x61;
            return true;
        }

        if (key >= 0x41 && key <= 0x5A)
        {
            index = 9 + key - 0x41;
            return true;
        }

        return false;
    }
}
