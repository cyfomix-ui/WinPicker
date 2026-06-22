using System.Drawing;

namespace WinPicker;

public sealed class WindowInfo
{
    public IntPtr Handle { get; init; }
    public string Title { get; init; } = string.Empty;
    public string ProcessName { get; init; } = string.Empty;
    public string ClassName { get; init; } = string.Empty;
    public Rectangle Bounds { get; init; }
    public bool IsMinimized { get; init; }
    public bool IsMaximized { get; init; }
    public int MonitorIndex { get; init; }

    public override string ToString()
    {
        return $"{Title} ({ProcessName}) hwnd=0x{Handle.ToInt64():X}";
    }
}
