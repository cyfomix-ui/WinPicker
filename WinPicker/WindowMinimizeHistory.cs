namespace WinPicker;

public sealed class WindowMinimizeHistory
{
    private readonly object _gate = new();
    private readonly HashSet<IntPtr> _windows = new();
    private readonly Dictionary<string, List<IntPtr>> _monitorWindows = new(StringComparer.OrdinalIgnoreCase);

    public void RecordWindow(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
            return;

        lock (_gate)
        {
            _windows.Add(handle);
        }
    }

    public bool HasWindow(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
            return false;

        lock (_gate)
        {
            return _windows.Contains(handle);
        }
    }

    public void RemoveWindow(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
            return;

        lock (_gate)
        {
            _windows.Remove(handle);
            foreach (var key in _monitorWindows.Keys.ToList())
            {
                _monitorWindows[key].RemoveAll(h => h == handle);
                if (_monitorWindows[key].Count == 0)
                    _monitorWindows.Remove(key);
            }
        }
    }

    public void RecordMonitor(string monitorKey, IEnumerable<IntPtr> handles)
    {
        if (string.IsNullOrWhiteSpace(monitorKey))
            return;

        var list = handles.Where(h => h != IntPtr.Zero).Distinct().ToList();
        if (list.Count == 0)
            return;

        lock (_gate)
        {
            _monitorWindows[monitorKey] = list;
            foreach (var handle in list)
                _windows.Add(handle);
        }
    }

    public bool HasMonitor(string monitorKey)
    {
        if (string.IsNullOrWhiteSpace(monitorKey))
            return false;

        lock (_gate)
        {
            return _monitorWindows.TryGetValue(monitorKey, out var handles) && handles.Count > 0;
        }
    }

    public List<IntPtr> TakeMonitor(string monitorKey)
    {
        if (string.IsNullOrWhiteSpace(monitorKey))
            return new List<IntPtr>();

        lock (_gate)
        {
            if (!_monitorWindows.TryGetValue(monitorKey, out var handles))
                return new List<IntPtr>();

            _monitorWindows.Remove(monitorKey);
            var result = handles.Distinct().ToList();
            foreach (var handle in result)
                _windows.Remove(handle);

            return result;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _windows.Clear();
            _monitorWindows.Clear();
        }
    }
}
