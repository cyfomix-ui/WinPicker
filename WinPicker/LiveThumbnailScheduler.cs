namespace WinPicker;

internal sealed class LiveThumbnailScheduler : IDisposable
{
    private static readonly TimeSpan MinimumRefreshAge = TimeSpan.FromMilliseconds(950);
    private static readonly TimeSpan FailureBackoff = TimeSpan.FromSeconds(3);

    private readonly Logger _logger;
    private readonly SynchronizationContext _uiContext;
    private readonly Action<int, IntPtr, Bitmap> _onCaptured;
    private readonly object _gate = new();
    private readonly Dictionary<IntPtr, WindowSnapshot> _targets = new();
    private readonly Dictionary<IntPtr, DateTime> _lastAttemptUtc = new();
    private readonly Dictionary<IntPtr, DateTime> _retryAfterUtc = new();
    private readonly HashSet<IntPtr> _inFlight = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;
    private readonly int _generation;
    private bool _disposed;

    public LiveThumbnailScheduler(int generation, Logger logger, SynchronizationContext uiContext, Action<int, IntPtr, Bitmap> onCaptured)
    {
        _generation = generation;
        _logger = logger;
        _uiContext = uiContext;
        _onCaptured = onCaptured;
        _worker = Task.Run(RunAsync);
    }

    public void UpdateTargets(IEnumerable<WindowSnapshot> targets)
    {
        if (_disposed)
            return;

        lock (_gate)
        {
            _targets.Clear();
            foreach (var target in targets)
                _targets[target.Handle] = target;

            foreach (var stale in _lastAttemptUtc.Keys.Except(_targets.Keys).ToArray())
            {
                _lastAttemptUtc.Remove(stale);
                _retryAfterUtc.Remove(stale);
            }
        }
    }

    private async Task RunAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                WindowSnapshot? target = null;
                var now = DateTime.UtcNow;
                lock (_gate)
                {
                    target = _targets.Values
                        .Where(candidate => !_inFlight.Contains(candidate.Handle))
                        .Where(candidate => !_retryAfterUtc.TryGetValue(candidate.Handle, out var retryAfter) || retryAfter <= now)
                        .Where(candidate => !_lastAttemptUtc.TryGetValue(candidate.Handle, out var last) || now - last >= MinimumRefreshAge)
                        .OrderByDescending(candidate => candidate.Priority)
                        .ThenBy(candidate => _lastAttemptUtc.TryGetValue(candidate.Handle, out var last) ? last : DateTime.MinValue)
                        .FirstOrDefault();

                    if (target is not null)
                    {
                        _inFlight.Add(target.Handle);
                        _lastAttemptUtc[target.Handle] = now;
                    }
                }

                if (target is null)
                {
                    await Task.Delay(100, _cts.Token).ConfigureAwait(false);
                    continue;
                }

                Bitmap? bitmap = null;
                try
                {
                    bitmap = WindowThumbnailCaptureWorker.Capture(target, _cts.Token);
                    if (bitmap is null)
                    {
                        lock (_gate)
                            _retryAfterUtc[target.Handle] = DateTime.UtcNow + FailureBackoff;
                    }
                    else
                    {
                        if (_cts.IsCancellationRequested)
                        {
                            bitmap.Dispose();
                            bitmap = null;
                            break;
                        }

                        var captured = bitmap;
                        var capturedHandle = target.Handle;
                        bitmap = null;
                        _uiContext.Post(_ => _onCaptured(_generation, capturedHandle, captured), null);
                    }
                }
                catch (OperationCanceledException) when (_cts.IsCancellationRequested)
                {
                    bitmap?.Dispose();
                    break;
                }
                catch (Exception ex)
                {
                    bitmap?.Dispose();
                    lock (_gate)
                        _retryAfterUtc[target.Handle] = DateTime.UtcNow + FailureBackoff;
                    _logger.Warn($"Live thumbnail capture failed. hwnd=0x{target.Handle.ToInt64():X} error={ex.Message}");
                }
                finally
                {
                    lock (_gate)
                        _inFlight.Remove(target.Handle);
                }

                // One worker, small slices: never enqueue an entire window set at once.
                await Task.Delay(120, _cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during map shutdown.
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cts.Cancel();
        lock (_gate)
            _targets.Clear();

        _ = _worker.ContinueWith(
            _ => _cts.Dispose(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
