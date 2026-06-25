namespace WinPicker;

public sealed class DesktopIconLayoutService
{
    private readonly Logger _logger;

    public DesktopIconLayoutService(Logger logger)
    {
        _logger = logger;
    }

    public List<DesktopIconSnapshot> Capture()
    {
        var result = new List<DesktopIconSnapshot>();

        try
        {
            var listView = FindDesktopListView();
            if (listView == IntPtr.Zero)
                return result;

            NativeMethods.GetWindowThreadProcessId(listView, out var processId);
            var process = NativeMethods.OpenProcess(
                NativeMethods.PROCESS_VM_OPERATION | NativeMethods.PROCESS_VM_READ | NativeMethods.PROCESS_VM_WRITE | NativeMethods.PROCESS_QUERY_INFORMATION,
                false,
                processId);

            if (process == IntPtr.Zero)
                return result;

            try
            {
                var count = NativeMethods.SendMessage(listView, NativeMethods.LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero).ToInt32();
                var remotePoint = NativeMethods.VirtualAllocEx(
                    process,
                    IntPtr.Zero,
                    new UIntPtr(8),
                    NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE,
                    NativeMethods.PAGE_READWRITE);

                if (remotePoint == IntPtr.Zero)
                    return result;

                try
                {
                    for (var i = 0; i < count; i++)
                    {
                        NativeMethods.SendMessage(listView, NativeMethods.LVM_GETITEMPOSITION, new IntPtr(i), remotePoint);
                        var buffer = new byte[8];
                        if (NativeMethods.ReadProcessMemory(process, remotePoint, buffer, buffer.Length, out _))
                        {
                            var x = BitConverter.ToInt32(buffer, 0);
                            var y = BitConverter.ToInt32(buffer, 4);
                            result.Add(new DesktopIconSnapshot { Index = i, X = x, Y = y });
                        }
                    }
                }
                finally
                {
                    NativeMethods.VirtualFreeEx(process, remotePoint, UIntPtr.Zero, NativeMethods.MEM_RELEASE);
                }
            }
            finally
            {
                NativeMethods.CloseHandle(process);
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Desktop icon capture failed: {ex.Message}");
        }

        _logger.Info($"Desktop icon positions captured: {result.Count}");
        return result;
    }

    public void Restore(List<DesktopIconSnapshot>? icons)
    {
        if (icons is null || icons.Count == 0)
            return;

        try
        {
            var listView = FindDesktopListView();
            if (listView == IntPtr.Zero)
                return;

            var count = NativeMethods.SendMessage(listView, NativeMethods.LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero).ToInt32();
            foreach (var icon in icons.OrderBy(i => i.Index))
            {
                if (icon.Index < 0 || icon.Index >= count)
                    continue;

                var lParam = MakeLParam(icon.X, icon.Y);
                NativeMethods.SendMessage(listView, NativeMethods.LVM_SETITEMPOSITION, new IntPtr(icon.Index), lParam);
            }

            _logger.Info($"Desktop icon positions restored: {icons.Count}");
        }
        catch (Exception ex)
        {
            _logger.Warn($"Desktop icon restore failed: {ex.Message}");
        }
    }

    private static IntPtr MakeLParam(int low, int high)
    {
        var value = (high & 0xFFFF) << 16 | (low & 0xFFFF);
        return new IntPtr(value);
    }

    private static IntPtr FindDesktopListView()
    {
        var progman = NativeMethods.FindWindow("Progman", null);
        var defView = NativeMethods.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (defView != IntPtr.Zero)
        {
            var list = NativeMethods.FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
            if (list != IntPtr.Zero)
                return list;
        }

        var worker = IntPtr.Zero;
        while (true)
        {
            worker = NativeMethods.FindWindowEx(IntPtr.Zero, worker, "WorkerW", null);
            if (worker == IntPtr.Zero)
                break;

            defView = NativeMethods.FindWindowEx(worker, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (defView == IntPtr.Zero)
                continue;

            var list = NativeMethods.FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
            if (list != IntPtr.Zero)
                return list;
        }

        return IntPtr.Zero;
    }
}

public sealed class DesktopIconSnapshot
{
    public int Index { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
}
