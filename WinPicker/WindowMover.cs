namespace WinPicker;

public sealed class WindowMover
{
    private readonly AppSettings _settings;
    private readonly Logger _logger;
    private readonly WindowMoveHistory _history;

    public WindowMover(AppSettings settings, Logger logger, WindowMoveHistory history)
    {
        _settings = settings;
        _logger = logger;
        _history = history;
    }

    public void Summon(WindowInfo window)
    {
        try
        {
            var screens = Screen.AllScreens;
            if (screens.Length == 0)
                return;

            var targetScreen = ResolveTargetScreen(screens, out var targetIndex, out var targetReason);
            var targetArea = targetScreen.WorkingArea;

            _logger.Info($"Summon hwnd=0x{window.Handle.ToInt64():X} title=\"{window.Title}\" from monitor={window.MonitorIndex} to monitor={targetIndex} reason={targetReason} device=\"{targetScreen.DeviceName}\"");

            var wasMaximized = window.IsMaximized;
            var wasMinimized = window.IsMinimized;
            var originalBounds = GetCurrentBounds(window.Handle, window.Bounds);
            _history.Record(window.Handle, originalBounds, wasMaximized, wasMinimized, window.Title);

            if (wasMinimized && _settings.RestoreMinimized)
            {
                NativeMethods.ShowWindow(window.Handle, NativeMethods.SW_RESTORE);
                Thread.Sleep(80);
            }
            else if (wasMaximized)
            {
                NativeMethods.ShowWindow(window.Handle, NativeMethods.SW_RESTORE);
                Thread.Sleep(80);
            }

            var bounds = GetCurrentBounds(window.Handle, window.Bounds);
            var width = Math.Min(Math.Max(bounds.Width, 320), targetArea.Width);
            var height = Math.Min(Math.Max(bounds.Height, 200), targetArea.Height);

            var x = targetArea.Left + (targetArea.Width - width) / 2;
            var y = targetArea.Top + (targetArea.Height - height) / 2;

            var moved = NativeMethods.SetWindowPos(
                window.Handle,
                NativeMethods.HWND_TOP,
                x,
                y,
                width,
                height,
                NativeMethods.SWP_SHOWWINDOW);

            if (!moved)
                _logger.Warn($"SetWindowPos failed hwnd=0x{window.Handle.ToInt64():X}");

            if (wasMaximized && _settings.KeepMaximized)
            {
                NativeMethods.ShowWindow(window.Handle, NativeMethods.SW_SHOWMAXIMIZED);
                Thread.Sleep(40);
            }

            BringToFront(window.Handle);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to summon window hwnd=0x{window.Handle.ToInt64():X}", ex);
        }
    }

    public bool RestoreLast()
    {
        try
        {
            if (!_history.TryTake(out var snapshot))
            {
                _logger.Warn("RestoreLast requested, but no move history exists.");
                return false;
            }

            if (snapshot.Handle == IntPtr.Zero)
                return false;

            _logger.Info($"RestoreLast hwnd=0x{snapshot.Handle.ToInt64():X} title=\"{snapshot.Title}\" bounds={snapshot.Bounds}");

            if (NativeMethods.IsIconic(snapshot.Handle) || NativeMethods.IsZoomed(snapshot.Handle))
            {
                NativeMethods.ShowWindow(snapshot.Handle, NativeMethods.SW_RESTORE);
                Thread.Sleep(80);
            }

            var moved = NativeMethods.SetWindowPos(
                snapshot.Handle,
                NativeMethods.HWND_TOP,
                snapshot.Bounds.Left,
                snapshot.Bounds.Top,
                snapshot.Bounds.Width,
                snapshot.Bounds.Height,
                NativeMethods.SWP_SHOWWINDOW);

            if (!moved)
                _logger.Warn($"RestoreLast SetWindowPos failed hwnd=0x{snapshot.Handle.ToInt64():X}");

            if (snapshot.WasMaximized && _settings.KeepMaximized)
            {
                NativeMethods.ShowWindow(snapshot.Handle, NativeMethods.SW_SHOWMAXIMIZED);
                Thread.Sleep(40);
                BringToFront(snapshot.Handle);
            }
            else if (snapshot.WasMinimized)
            {
                NativeMethods.ShowWindow(snapshot.Handle, NativeMethods.SW_MINIMIZE);
                Thread.Sleep(40);
                _logger.Info($"RestoreLast minimized hwnd=0x{snapshot.Handle.ToInt64():X}");
            }
            else
            {
                BringToFront(snapshot.Handle);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("RestoreLast failed.", ex);
            return false;
        }
    }

    public Screen ResolveTargetScreenForDisplay()
    {
        var screens = Screen.AllScreens;
        if (screens.Length == 0)
            return Screen.PrimaryScreen ?? Screen.FromPoint(Cursor.Position);

        return ResolveTargetScreen(screens, out _, out _);
    }

    private Screen ResolveTargetScreen(Screen[] screens, out int targetIndex, out string reason)
    {
        if (!string.IsNullOrWhiteSpace(_settings.TargetMonitorDeviceName))
        {
            for (var i = 0; i < screens.Length; i++)
            {
                if (string.Equals(screens[i].DeviceName, _settings.TargetMonitorDeviceName, StringComparison.OrdinalIgnoreCase))
                {
                    targetIndex = i;
                    reason = "TargetMonitorDeviceName";
                    return screens[i];
                }
            }
        }

        if (_settings.UsePrimaryScreen && Screen.PrimaryScreen is not null)
        {
            for (var i = 0; i < screens.Length; i++)
            {
                if (screens[i].DeviceName == Screen.PrimaryScreen.DeviceName)
                {
                    targetIndex = i;
                    reason = "PrimaryScreen";
                    return screens[i];
                }
            }

            targetIndex = Array.IndexOf(screens, Screen.PrimaryScreen);
            if (targetIndex < 0)
                targetIndex = 0;

            reason = "PrimaryScreen";
            return Screen.PrimaryScreen;
        }

        targetIndex = Math.Clamp(_settings.MainMonitorIndex, 0, screens.Length - 1);
        reason = "MainMonitorIndex";
        return screens[targetIndex];
    }

    private void BringToFront(IntPtr hWnd)
    {
        try
        {
            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);

            var foreground = NativeMethods.SetForegroundWindow(hWnd);
            if (foreground)
            {
                _logger.Info($"SetForegroundWindow result=True hwnd=0x{hWnd.ToInt64():X}");
                return;
            }

            _logger.Warn($"SetForegroundWindow result=False hwnd=0x{hWnd.ToInt64():X}. Trying TOPMOST pulse.");

            NativeMethods.SetWindowPos(
                hWnd,
                NativeMethods.HWND_TOPMOST,
                0,
                0,
                0,
                0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_SHOWWINDOW);

            NativeMethods.SetWindowPos(
                hWnd,
                NativeMethods.HWND_NOTOPMOST,
                0,
                0,
                0,
                0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_SHOWWINDOW);

            NativeMethods.SetForegroundWindow(hWnd);
        }
        catch (Exception ex)
        {
            _logger.Error($"BringToFront failed hwnd=0x{hWnd.ToInt64():X}", ex);
        }
    }

    private static Rectangle GetCurrentBounds(IntPtr hWnd, Rectangle fallback)
    {
        if (NativeMethods.IsIconic(hWnd))
        {
            var placement = new NativeMethods.WINDOWPLACEMENT
            {
                Length = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.WINDOWPLACEMENT>()
            };

            if (NativeMethods.GetWindowPlacement(hWnd, ref placement))
            {
                var normalBounds = placement.NormalPosition.ToRectangle();
                if (normalBounds.Width > 0 && normalBounds.Height > 0)
                    return normalBounds;
            }
        }

        if (NativeMethods.GetWindowRect(hWnd, out var rect) && rect.Width > 0 && rect.Height > 0)
            return rect.ToRectangle();

        return fallback;
    }
}
