namespace WinPicker;

public sealed class GeometrySnapshotService
{
    private const string RegistryPath = @"Software\Cyfomix\WinPicker\GeometrySnapshots";
    private const string ValueName = "SnapshotsJson";
    private const int MaxSnapshots = 8;

    private readonly Logger _logger;
    private readonly DesktopIconLayoutService _desktopIcons;

    public GeometrySnapshotService(Logger logger)
    {
        _logger = logger;
        _desktopIcons = new DesktopIconLayoutService(logger);
    }

    public List<GeometrySnapshot> LoadSnapshots()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            var json = key?.GetValue(ValueName) as string;
            if (string.IsNullOrWhiteSpace(json))
                return new List<GeometrySnapshot>();

            return JsonSerializer.Deserialize<List<GeometrySnapshot>>(json) ?? new List<GeometrySnapshot>();
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to load geometry snapshots: {ex.Message}");
            return new List<GeometrySnapshot>();
        }
    }

    public GeometrySnapshot Save(string? name, IReadOnlyList<WindowInfo> windows)
    {
        var snapshots = LoadSnapshots();
        var now = DateTime.Now;
        var finalName = string.IsNullOrWhiteSpace(name) ? now.ToString("yyyyMMdd_HHmmss") : name.Trim();

        var snapshot = new GeometrySnapshot
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = finalName,
            CreatedAt = now,
            Windows = windows.Select(w => new WindowGeometrySnapshot
            {
                Title = w.Title,
                ProcessName = w.ProcessName,
                ClassName = w.ClassName,
                Bounds = RectDto.FromRectangle(w.Bounds),
                IsMinimized = w.IsMinimized,
                IsMaximized = w.IsMaximized,
                MonitorIndex = w.MonitorIndex,
                MonitorDeviceName = TryGetMonitorDeviceName(w.Bounds)
            }).ToList(),
            DesktopIcons = _desktopIcons.Capture()
        };

        snapshots.RemoveAll(s => string.Equals(s.Name, finalName, StringComparison.OrdinalIgnoreCase));
        snapshots.Insert(0, snapshot);

        while (snapshots.Count > MaxSnapshots)
            snapshots.RemoveAt(snapshots.Count - 1);

        SaveSnapshots(snapshots);
        _logger.Info($"Geometry snapshot saved: {finalName}, windows={snapshot.Windows.Count}, icons={snapshot.DesktopIcons.Count}");
        return snapshot;
    }

    public void RestoreWindows(GeometrySnapshot snapshot, WindowEnumerator enumerator)
    {
        var currentWindows = enumerator.Enumerate();
        var used = new HashSet<IntPtr>();

        foreach (var saved in snapshot.Windows)
        {
            var target = FindMatchingWindow(saved, currentWindows, used);
            if (target is null)
                continue;

            used.Add(target.Handle);
            RestoreWindow(target.Handle, saved);
        }

        _logger.Info($"Geometry snapshot windows restored: {snapshot.Name}");
    }

    public void RestoreDesktopIcons(GeometrySnapshot snapshot)
    {
        _desktopIcons.Restore(snapshot.DesktopIcons);
        _logger.Info($"Geometry snapshot desktop icons restored: {snapshot.Name}");
    }

    private void SaveSnapshots(List<GeometrySnapshot> snapshots)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
        var json = JsonSerializer.Serialize(snapshots, new JsonSerializerOptions { WriteIndented = false });
        key?.SetValue(ValueName, json, RegistryValueKind.String);
    }

    private static WindowInfo? FindMatchingWindow(WindowGeometrySnapshot saved, IReadOnlyList<WindowInfo> current, HashSet<IntPtr> used)
    {
        WindowInfo? Match(Func<WindowInfo, bool> predicate) =>
            current.FirstOrDefault(w => !used.Contains(w.Handle) && predicate(w));

        return Match(w =>
                   string.Equals(w.ProcessName, saved.ProcessName, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(w.ClassName, saved.ClassName, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(w.Title, saved.Title, StringComparison.Ordinal)) ??
               Match(w =>
                   string.Equals(w.ProcessName, saved.ProcessName, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(w.Title, saved.Title, StringComparison.Ordinal)) ??
               Match(w =>
                   string.Equals(w.ProcessName, saved.ProcessName, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(w.ClassName, saved.ClassName, StringComparison.OrdinalIgnoreCase));
    }

    private void RestoreWindow(IntPtr hWnd, WindowGeometrySnapshot saved)
    {
        try
        {
            if (NativeMethods.IsIconic(hWnd) || NativeMethods.IsZoomed(hWnd))
            {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
                Thread.Sleep(60);
            }

            var rect = saved.Bounds.ToRectangle();
            if (rect.Width <= 0 || rect.Height <= 0)
                return;

            var moved = NativeMethods.SetWindowPos(
                hWnd,
                NativeMethods.HWND_TOP,
                rect.Left,
                rect.Top,
                rect.Width,
                rect.Height,
                NativeMethods.SWP_SHOWWINDOW);

            if (!moved)
                NativeMethods.MoveWindow(hWnd, rect.Left, rect.Top, rect.Width, rect.Height, true);

            Thread.Sleep(30);

            if (saved.IsMaximized)
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOWMAXIMIZED);
            else if (saved.IsMinimized)
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_MINIMIZE);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to restore window geometry hwnd=0x{hWnd.ToInt64():X}: {ex.Message}");
        }
    }

    private static string? TryGetMonitorDeviceName(Rectangle bounds)
    {
        try
        {
            var center = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
            return Screen.AllScreens.FirstOrDefault(s => s.Bounds.Contains(center))?.DeviceName;
        }
        catch
        {
            return null;
        }
    }
}

public sealed class GeometrySnapshot
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public List<WindowGeometrySnapshot> Windows { get; set; } = new();
    public List<DesktopIconSnapshot> DesktopIcons { get; set; } = new();
}

public sealed class WindowGeometrySnapshot
{
    public string Title { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public string ClassName { get; set; } = "";
    public RectDto Bounds { get; set; } = new();
    public bool IsMinimized { get; set; }
    public bool IsMaximized { get; set; }
    public int MonitorIndex { get; set; }
    public string? MonitorDeviceName { get; set; }
}

public sealed class RectDto
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public static RectDto FromRectangle(Rectangle rect) => new()
    {
        X = rect.X,
        Y = rect.Y,
        Width = rect.Width,
        Height = rect.Height
    };

    public Rectangle ToRectangle() => new(X, Y, Width, Height);
}
