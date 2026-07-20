using System.Runtime.InteropServices;

namespace WinPicker;

internal sealed class PickerOutsideClickCloser : IDisposable
{
    private readonly Form _form;
    private readonly Func<bool> _shouldIgnore;
    private readonly Logger _logger;
    private readonly NativeMethods.LowLevelMouseProc _proc;
    private readonly SynchronizationContext? _syncContext;
    private readonly Thread _hookThread;
    private readonly ManualResetEventSlim _ready = new(false);

    private IntPtr _hookHandle;
    private uint _hookThreadId;
    private bool _closing;
    private bool _disposed;

    public PickerOutsideClickCloser(Form form, Func<bool> shouldIgnore, Logger logger)
    {
        _form = form;
        _shouldIgnore = shouldIgnore;
        _logger = logger;
        _syncContext = SynchronizationContext.Current;
        _proc = HookCallback;
        _hookThread = new Thread(HookThreadMain)
        {
            IsBackground = true,
            Name = "WinPicker outside-click hook"
        };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();
    }

    private void HookThreadMain()
    {
        try
        {
            _hookThreadId = NativeMethods.GetCurrentThreadId();
            _hookHandle = NativeMethods.SetWindowsHookExMouse(
                NativeMethods.WH_MOUSE_LL,
                _proc,
                NativeMethods.GetModuleHandle(null),
                0);
            _ready.Set();

            if (_hookHandle == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                Post(() => _logger.Warn($"Picker outside-click hook registration failed. Win32Error={error}"));
                return;
            }

            Post(() => _logger.Info("Picker outside-click hook registered on dedicated STA thread."));
            Application.Run();
        }
        catch (Exception ex)
        {
            _ready.Set();
            Post(() => _logger.Error("Picker outside-click hook thread failed.", ex));
        }
        finally
        {
            if (_hookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0 && !_closing && IsMouseDownMessage(wParam.ToInt32()))
            {
                var cursor = Cursor.Position;
                if (!_form.IsDisposed && _form.Visible && !_shouldIgnore() && !_form.Bounds.Contains(cursor))
                {
                    _closing = true;
                    Post(() =>
                    {
                        try
                        {
                            if (!_form.IsDisposed)
                                _form.Close();
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("Failed to close picker from outside-click hook.", ex);
                        }
                    });
                }
            }
        }
        catch
        {
            // Keep the low-level callback non-blocking and free of file I/O.
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private static bool IsMouseDownMessage(int message)
    {
        return message is NativeMethods.WM_LBUTTONDOWN
            or NativeMethods.WM_RBUTTONDOWN
            or NativeMethods.WM_MBUTTONDOWN
            or NativeMethods.WM_XBUTTONDOWN;
    }

    private void Post(Action action)
    {
        if (_syncContext is not null)
            _syncContext.Post(_ => action(), null);
        else if (!_form.IsDisposed)
            _form.BeginInvoke(action);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        try
        {
            if (_hookThreadId != 0)
                NativeMethods.PostThreadMessage(_hookThreadId, NativeMethods.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            _ = Task.Run(() =>
            {
                if (_hookThread.IsAlive)
                    _hookThread.Join(TimeSpan.FromSeconds(2));
                _ready.Dispose();
            });
            _logger.Info("Picker outside-click hook stop requested.");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to stop picker outside-click hook thread.", ex);
        }
    }
}
