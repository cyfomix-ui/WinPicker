namespace WinPicker;

public sealed class WindowThumbnailCache : IDisposable
{
    private readonly Dictionary<IntPtr, Bitmap> _cache = new();

    public WindowThumbnailCache(Logger logger)
    {
        // Kept for constructor compatibility; capture logging lives in the scheduler/worker.
    }

    public Bitmap? TryGet(IntPtr handle)
    {
        return _cache.TryGetValue(handle, out var bitmap) ? bitmap : null;
    }

    // Called only on the map UI thread. The old bitmap is never disposed by a worker while it is being painted.
    public void Swap(IntPtr handle, Bitmap bitmap)
    {
        if (_cache.Remove(handle, out var old))
            old.Dispose();
        _cache[handle] = bitmap;
    }

    public void Prune(IEnumerable<IntPtr> liveHandles)
    {
        var keep = liveHandles.ToHashSet();
        foreach (var stale in _cache.Keys.Where(handle => !keep.Contains(handle)).ToArray())
        {
            _cache[stale].Dispose();
            _cache.Remove(stale);
        }
    }

    public void Clear()
    {
        foreach (var bitmap in _cache.Values)
            bitmap.Dispose();
        _cache.Clear();
    }

    public void Dispose() => Clear();
}
