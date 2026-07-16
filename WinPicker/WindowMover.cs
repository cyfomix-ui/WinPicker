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

    public bool ToggleSummon(WindowInfo window, out string status)
    {
        status = string.Empty;

        try
        {
            var screens = Screen.AllScreens;
            if (screens.Length == 0)
                return false;

            var targetScreen = ResolveTargetScreen(screens, out var targetIndex, out var targetReason);
            var currentBounds = GetCurrentBounds(window.Handle, window.Bounds);

            if (IsBoundsOnScreen(currentBounds, targetScreen))
            {
                if (_history.TryTake(window.Handle, out var snapshot))
                {
                    _logger.Info($"ToggleSummon restore hwnd=0x{window.Handle.ToInt64():X} title=\"{window.Title}\"");
                    RestoreSnapshot(snapshot, "ToggleRestore");
                    status = UiText.AltItemRestored(window.Title);
                    return true;
                }

                BringToFront(window.Handle);
                status = UiText.NoWindowRestoreHistory(window.Title);
                _logger.Info($"ToggleSummon target window already on target monitor, but no per-window history exists. hwnd=0x{window.Handle.ToInt64():X}");
                return false;
            }

            _logger.Info($"ToggleSummon move hwnd=0x{window.Handle.ToInt64():X} title=\"{window.Title}\" to monitor={targetIndex} reason={targetReason}");
            Summon(window);
            status = UiText.AltItemMoved(window.Title);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"ToggleSummon failed hwnd=0x{window.Handle.ToInt64():X}", ex);
            status = UiText.AltItemFailed(window.Title);
            return false;
        }
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

            if (_settings.UseSummonSize)
            {
                width = Math.Clamp(_settings.SummonWidth, 320, targetArea.Width);
                height = Math.Clamp(_settings.SummonHeight, 200, targetArea.Height);
            }

            var x = targetArea.Left + (targetArea.Width - width) / 2;
            var y = targetArea.Top + (targetArea.Height - height) / 2;

            var moved = MoveWindowRobust(window.Handle, x, y, width, height, "Summon");

            if (wasMaximized && _settings.KeepMaximized && !_settings.UseSummonSize)
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

            RestoreSnapshot(snapshot, "RestoreLast");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("RestoreLast failed.", ex);
            return false;
        }
    }

    private void RestoreSnapshot(WindowMoveSnapshot snapshot, string source)
    {
        if (snapshot.Handle == IntPtr.Zero)
            return;

        _logger.Info($"{source} hwnd=0x{snapshot.Handle.ToInt64():X} title=\"{snapshot.Title}\" bounds={snapshot.Bounds}");

        if (NativeMethods.IsIconic(snapshot.Handle) || NativeMethods.IsZoomed(snapshot.Handle))
        {
            NativeMethods.ShowWindow(snapshot.Handle, NativeMethods.SW_RESTORE);
            Thread.Sleep(80);
        }

        MoveWindowRobust(
            snapshot.Handle,
            snapshot.Bounds.Left,
            snapshot.Bounds.Top,
            snapshot.Bounds.Width,
            snapshot.Bounds.Height,
            source);

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
            _logger.Info($"{source} minimized hwnd=0x{snapshot.Handle.ToInt64():X}");
        }
        else
        {
            BringToFront(snapshot.Handle);
        }
    }

    private static bool IsBoundsOnScreen(Rectangle bounds, Screen screen)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return false;

        var center = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
        if (screen.Bounds.Contains(center))
            return true;

        var intersection = Rectangle.Intersect(bounds, screen.Bounds);
        return intersection.Width > 0 && intersection.Height > 0 &&
               intersection.Width * intersection.Height >= Math.Max(1, bounds.Width * bounds.Height / 2);
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

    private bool MoveWindowRobust(IntPtr hWnd, int x, int y, int width, int height, string source)
    {
        try
        {
            // Some utility windows resist one of the movement APIs. Try SetWindowPos first,
            // then MoveWindow, then SetWindowPos again without changing Z-order.
            var moved = NativeMethods.SetWindowPos(
                hWnd,
                NativeMethods.HWND_TOP,
                x,
                y,
                width,
                height,
                NativeMethods.SWP_SHOWWINDOW);

            if (!moved)
            {
                var error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                _logger.Warn($"{source} SetWindowPos failed hwnd=0x{hWnd.ToInt64():X} err={error}. Trying MoveWindow.");
            }

            if (!moved)
            {
                moved = NativeMethods.MoveWindow(hWnd, x, y, width, height, true);
                if (!moved)
                {
                    var error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                    _logger.Warn($"{source} MoveWindow failed hwnd=0x{hWnd.ToInt64():X} err={error}. Trying SetWindowPos without Z-order.");
                }
            }

            if (!moved)
            {
                moved = NativeMethods.SetWindowPos(
                    hWnd,
                    IntPtr.Zero,
                    x,
                    y,
                    width,
                    height,
                    NativeMethods.SWP_NOZORDER | NativeMethods.SWP_SHOWWINDOW);
            }

            Thread.Sleep(30);

            var after = GetCurrentBounds(hWnd, Rectangle.Empty);
            var targetPoint = new Point(x + Math.Max(1, width / 2), y + Math.Max(1, height / 2));
            var movedToTarget = after.Width > 0 &&
                                after.Height > 0 &&
                                after.Left <= targetPoint.X &&
                                after.Right >= targetPoint.X &&
                                after.Top <= targetPoint.Y &&
                                after.Bottom >= targetPoint.Y;

            _logger.Info($"{source} move result hwnd=0x{hWnd.ToInt64():X} moved={moved} after={after} target=({x},{y},{width},{height}) movedToTarget={movedToTarget}");

            if (!movedToTarget)
            {
                _logger.Warn($"{source} window did not appear to move to target. If this is an elevated/admin app such as a disk utility, run WinPicker as administrator too.");
            }

            return moved;
        }
        catch (Exception ex)
        {
            _logger.Error($"{source} MoveWindowRobust failed hwnd=0x{hWnd.ToInt64():X}", ex);
            return false;
        }
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
