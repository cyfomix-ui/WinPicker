namespace WinPicker;

public sealed record WindowMoveSnapshot(IntPtr Handle, Rectangle Bounds, bool WasMaximized, bool WasMinimized, string Title);

public sealed class WindowMoveHistory
{
    private readonly object _sync = new();
    private WindowMoveSnapshot? _last;

    public bool HasLast
    {
        get
        {
            lock (_sync)
                return _last is not null;
        }
    }

    public void Record(IntPtr handle, Rectangle bounds, bool wasMaximized, bool wasMinimized, string title)
    {
        lock (_sync)
            _last = new WindowMoveSnapshot(handle, bounds, wasMaximized, wasMinimized, title);
    }

    public bool TryTake(out WindowMoveSnapshot snapshot)
    {
        lock (_sync)
        {
            if (_last is null)
            {
                snapshot = default!;
                return false;
            }

            snapshot = _last;
            _last = null;
            return true;
        }
    }
}
