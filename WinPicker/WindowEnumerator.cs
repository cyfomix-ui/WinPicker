using System.Diagnostics;
using System.Text;

namespace WinPicker;

public sealed class WindowEnumerator
{
    private readonly AppSettings _settings;
    private readonly Logger _logger;

    public WindowEnumerator(AppSettings settings, Logger logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public List<WindowInfo> Enumerate()
    {
        var result = new List<WindowInfo>();
        var currentProcessId = Environment.ProcessId;
        var screens = Screen.AllScreens;
        var zOrderIndex = 0;

        try
        {
            NativeMethods.EnumWindows((hWnd, _) =>
            {
                try
                {
                    var info = TryCreateWindowInfo(hWnd, currentProcessId, screens, zOrderIndex);
                    zOrderIndex++;

                    if (info is not null)
                        result.Add(info);
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Failed to inspect window hwnd=0x{hWnd.ToInt64():X}: {ex.Message}");
                }

                return true;
            }, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            _logger.Error("EnumWindows failed.", ex);
        }

        // EnumWindows returns top-level windows in Z-order from topmost/front to back.
        // Preserve that order for the right-side list and draw the map back-to-front.
        // Sorting by monitor/process/title makes the minimap Z-order visually wrong.
        // ZOrderIndex is assigned while creating WindowInfo.
        return result;
    }

    /// <summary>
    /// Lightweight top-level window enumeration used only by media suppression.
    /// It intentionally skips elevation/token checks and monitor-index calculation.
    /// </summary>
    public List<WindowInfo> EnumerateForMediaDetection(CancellationToken cancellationToken)
    {
        var result = new List<WindowInfo>();
        var currentProcessId = Environment.ProcessId;
        var zOrderIndex = 0;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            NativeMethods.EnumWindows((hWnd, _) =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return false;

                try
                {
                    var info = TryCreateMediaWindowInfo(hWnd, currentProcessId, zOrderIndex);
                    zOrderIndex++;

                    if (info is not null)
                        result.Add(info);
                }
                catch (Exception ex)
                {
                    _logger.Debug($"Failed to inspect media window hwnd=0x{hWnd.ToInt64():X}: {ex.Message}");
                }

                return true;
            }, IntPtr.Zero);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Warn($"Media EnumWindows failed: {ex.Message}");
        }

        return result;
    }

    private WindowInfo? TryCreateMediaWindowInfo(IntPtr hWnd, int currentProcessId, int zOrderIndex)
    {
        if (hWnd == IntPtr.Zero || !NativeMethods.IsWindowVisible(hWnd) || IsCloaked(hWnd))
            return null;

        var className = GetClassName(hWnd);
        if (IsExcludedWindowClass(className))
            return null;

        var title = GetTitle(hWnd).Trim();
        if (string.IsNullOrWhiteSpace(title))
            return null;

        NativeMethods.GetWindowThreadProcessId(hWnd, out var processIdValue);
        var processId = (int)processIdValue;
        if (processId == currentProcessId)
            return null;

        var processName = GetProcessName(processId);
        if (IsExcluded(processName, title))
            return null;

        var isMinimized = NativeMethods.IsIconic(hWnd);
        var bounds = isMinimized ? GetNormalBounds(hWnd) : GetRealBounds(hWnd);
        if (bounds.Width < 80 || bounds.Height < 60)
            bounds = GetRealBounds(hWnd);
        if (bounds.Width < 80 || bounds.Height < 60)
            return null;

        var exStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
        if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) == NativeMethods.WS_EX_TOOLWINDOW)
            return null;

        return new WindowInfo
        {
            Handle = hWnd,
            Title = title,
            ProcessName = processName,
            ClassName = className,
            Bounds = bounds,
            IsMinimized = isMinimized,
            IsMaximized = false,
            IsElevated = false,
            MonitorIndex = -1,
            ZOrderIndex = zOrderIndex
        };
    }

    private WindowInfo? TryCreateWindowInfo(IntPtr hWnd, int currentProcessId, Screen[] screens, int zOrderIndex)
    {
        if (hWnd == IntPtr.Zero)
            return null;

        if (!NativeMethods.IsWindowVisible(hWnd))
            return null;

        if (IsCloaked(hWnd))
            return null;

        var className = GetClassName(hWnd);
        if (IsExcludedWindowClass(className))
            return null;

        var title = GetTitle(hWnd).Trim();
        if (string.IsNullOrWhiteSpace(title))
            return null;

        NativeMethods.GetWindowThreadProcessId(hWnd, out var processIdValue);
        var processId = (int)processIdValue;
        if (processId == currentProcessId)
            return null;

        var processName = GetProcessName(processId);
        var isElevated = IsProcessElevated(processId);
        if (IsExcluded(processName, title))
            return null;

        var isMinimized = NativeMethods.IsIconic(hWnd);
        var bounds = isMinimized ? GetNormalBounds(hWnd) : GetRealBounds(hWnd);

        if (bounds.Width < 80 || bounds.Height < 60)
            bounds = GetRealBounds(hWnd);

        if (bounds.Width < 80 || bounds.Height < 60)
            return null;

        var exStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
        if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) == NativeMethods.WS_EX_TOOLWINDOW)
            return null;

        var monitorIndex = GetMonitorIndex(bounds, screens);

        return new WindowInfo
        {
            Handle = hWnd,
            Title = title,
            ProcessName = processName,
            ClassName = className,
            Bounds = bounds,
            IsMinimized = isMinimized,
            IsMaximized = NativeMethods.IsZoomed(hWnd),
            IsElevated = isElevated,
            MonitorIndex = monitorIndex,
            ZOrderIndex = zOrderIndex
        };
    }


    private bool IsProcessElevated(int processId)
    {
        IntPtr processHandle = IntPtr.Zero;
        IntPtr tokenHandle = IntPtr.Zero;

        try
        {
            processHandle = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)processId);
            if (processHandle == IntPtr.Zero)
                return false;

            if (!NativeMethods.OpenProcessToken(processHandle, NativeMethods.TOKEN_QUERY, out tokenHandle))
                return false;

            var elevation = new NativeMethods.TOKEN_ELEVATION();
            var ok = NativeMethods.GetTokenInformation(
                tokenHandle,
                NativeMethods.TokenElevation,
                out elevation,
                Marshal.SizeOf<NativeMethods.TOKEN_ELEVATION>(),
                out _);

            return ok && elevation.TokenIsElevated != 0;
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to check elevation for processId={processId}: {ex.Message}");
            return false;
        }
        finally
        {
            if (tokenHandle != IntPtr.Zero)
                NativeMethods.CloseHandle(tokenHandle);
            if (processHandle != IntPtr.Zero)
                NativeMethods.CloseHandle(processHandle);
        }
    }

    private bool IsExcluded(string processName, string title)
    {
        if (_settings.ExcludeProcesses.Any(p => string.Equals(p, processName, StringComparison.OrdinalIgnoreCase)))
            return true;

        if (_settings.ExcludeWindowTitles.Any(t => title.Contains(t, StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }

    private static string GetTitle(IntPtr hWnd)
    {
        var length = NativeMethods.GetWindowTextLength(hWnd);
        if (length <= 0)
            return string.Empty;

        var builder = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(hWnd, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string GetClassName(IntPtr hWnd)
    {
        try
        {
            var builder = new StringBuilder(256);
            return NativeMethods.GetClassName(hWnd, builder, builder.Capacity) > 0
                ? builder.ToString()
                : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsExcludedWindowClass(string className)
    {
        if (string.IsNullOrWhiteSpace(className))
            return false;

        var excluded = new[]
        {
            "Shell_TrayWnd",
            "Shell_SecondaryTrayWnd",
            "Progman",
            "WorkerW",
            "NotifyIconOverflowWindow",
            "Windows.UI.Core.CoreWindow"
        };

        return excluded.Any(c => string.Equals(c, className, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetProcessName(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsCloaked(IntPtr hWnd)
    {
        try
        {
            var hr = NativeMethods.DwmGetWindowAttribute(
                hWnd,
                NativeMethods.DWMWA_CLOAKED,
                out int cloaked,
                sizeof(int));

            return hr == 0 && cloaked != 0;
        }
        catch
        {
            return false;
        }
    }

    private static Rectangle GetRealBounds(IntPtr hWnd)
    {
        try
        {
            var hr = NativeMethods.DwmGetWindowAttribute(
                hWnd,
                NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
                out NativeMethods.RECT rect,
                System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.RECT>());

            if (hr == 0 && rect.Width > 0 && rect.Height > 0)
                return rect.ToRectangle();
        }
        catch
        {
            // Fall through to GetWindowRect.
        }

        return NativeMethods.GetWindowRect(hWnd, out var fallback)
            ? fallback.ToRectangle()
            : Rectangle.Empty;
    }

    private static Rectangle GetNormalBounds(IntPtr hWnd)
    {
        try
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
        catch
        {
            // Fall through to real bounds.
        }

        return GetRealBounds(hWnd);
    }

    private static int GetMonitorIndex(Rectangle bounds, Screen[] screens)
    {
        if (screens.Length == 0)
            return 0;

        var center = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
        for (var i = 0; i < screens.Length; i++)
        {
            if (screens[i].Bounds.Contains(center))
                return i;
        }

        var nearest = 0;
        var nearestArea = 0;
        for (var i = 0; i < screens.Length; i++)
        {
            var intersection = Rectangle.Intersect(bounds, screens[i].Bounds);
            var area = intersection.Width * intersection.Height;
            if (area > nearestArea)
            {
                nearestArea = area;
                nearest = i;
            }
        }

        return nearest;
    }
}
