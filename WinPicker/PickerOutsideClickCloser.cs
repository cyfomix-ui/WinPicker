using System.Runtime.InteropServices;

namespace WinPicker;

internal sealed class PickerOutsideClickCloser : IDisposable
{
    private readonly Form _form;
    private readonly Func<bool> _shouldIgnore;
    private readonly Logger _logger;
    private readonly NativeMethods.LowLevelMouseProc _proc;
    private readonly SynchronizationContext? _syncContext;

    private IntPtr _hookHandle;
    private bool _closing;
    private bool _disposed;

    public PickerOutsideClickCloser(Form form, Func<bool> shouldIgnore, Logger logger)
    {
        _form = form;
        _shouldIgnore = shouldIgnore;
        _logger = logger;
        _syncContext = SynchronizationContext.Current;
        _proc = HookCallback;

        Install();
    }

    private void Install()
    {
        try
        {
            _hookHandle = NativeMethods.SetWindowsHookExMouse(
                NativeMethods.WH_MOUSE_LL,
                _proc,
                NativeMethods.GetModuleHandle(null),
                0);

            if (_hookHandle == IntPtr.Zero)
            {
                var win32Error = Marshal.GetLastWin32Error();
                _logger.Warn($"Picker outside-click hook registration failed. Win32Error={win32Error}");
                return;
            }

            _logger.Info("Picker outside-click hook registered.");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to register picker outside-click hook.", ex);
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0 && !_closing && IsMouseDownMessage(wParam.ToInt32()))
            {
                var cursor = Cursor.Position;

                if (!_form.IsDisposed &&
                    _form.Visible &&
                    !_shouldIgnore() &&
                    !_form.Bounds.Contains(cursor))
                {
                    _closing = true;
                    _logger.Info($"Picker outside click detected at {cursor}. Closing picker.");

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
        catch (Exception ex)
        {
            _logger.Error("Picker outside-click hook callback failed.", ex);
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
            if (_hookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
                _logger.Info("Picker outside-click hook unregistered.");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to unregister picker outside-click hook.", ex);
        }
    }
}
