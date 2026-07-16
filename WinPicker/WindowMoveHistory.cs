namespace WinPicker;

public sealed record WindowMoveSnapshot(IntPtr Handle, Rectangle Bounds, bool WasMaximized, bool WasMinimized, string Title);

public sealed class WindowMoveHistory
{
    private readonly object _sync = new();
    private readonly Dictionary<IntPtr, WindowMoveSnapshot> _byHandle = new();
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
        {
            var snapshot = new WindowMoveSnapshot(handle, bounds, wasMaximized, wasMinimized, title);
            _last = snapshot;
            _byHandle[handle] = snapshot;
        }
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
            _byHandle.Remove(snapshot.Handle);
            return true;
        }
    }

    public bool TryTake(IntPtr handle, out WindowMoveSnapshot snapshot)
    {
        lock (_sync)
        {
            if (!_byHandle.TryGetValue(handle, out snapshot!))
                return false;

            _byHandle.Remove(handle);
            if (_last?.Handle == handle)
                _last = null;

            return true;
        }
    }
}
