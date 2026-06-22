using System.Reflection;
using System.Runtime.InteropServices;

namespace WinPicker;

internal static class NotifyIconLocator
{
    public static bool TryGetIconCenter(NotifyIcon notifyIcon, Logger logger, out Point center)
    {
        center = Point.Empty;

        try
        {
            if (!TryGetNotifyIconIdentity(notifyIcon, out var hwnd, out var id))
                return false;

            if (hwnd == IntPtr.Zero)
                return false;

            var identifier = new NOTIFYICONIDENTIFIER
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONIDENTIFIER>(),
                hWnd = hwnd,
                uID = id,
                guidItem = Guid.Empty
            };

            var hr = Shell_NotifyIconGetRect(ref identifier, out var rect);
            if (hr != 0)
            {
                logger.Warn($"Shell_NotifyIconGetRect failed. HRESULT=0x{hr:X8}");
                return false;
            }

            var r = rect.ToRectangle();
            if (r.Width <= 0 || r.Height <= 0)
                return false;

            center = new Point(r.Left + r.Width / 2, r.Top + r.Height / 2);
            return true;
        }
        catch (Exception ex)
        {
            logger.Error("Failed to locate WinPicker tray icon.", ex);
            return false;
        }
    }

    private static bool TryGetNotifyIconIdentity(NotifyIcon notifyIcon, out IntPtr hwnd, out uint id)
    {
        hwnd = IntPtr.Zero;
        id = 0;

        var type = notifyIcon.GetType();
        var windowObj = GetFieldValue(type, notifyIcon, "_window") ?? GetFieldValue(type, notifyIcon, "window");
        var idObj = GetFieldValue(type, notifyIcon, "_id") ?? GetFieldValue(type, notifyIcon, "id");

        if (windowObj is null || idObj is null)
            return false;

        id = Convert.ToUInt32(idObj);

        if (windowObj is NativeWindow nativeWindow)
        {
            hwnd = nativeWindow.Handle;
            return hwnd != IntPtr.Zero;
        }

        var windowType = windowObj.GetType();
        var handleProp = windowType.GetProperty("Handle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (handleProp?.GetValue(windowObj) is IntPtr propHandle)
        {
            hwnd = propHandle;
            return hwnd != IntPtr.Zero;
        }

        var handleField = windowType.GetField("Handle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? windowType.GetField("_handle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? windowType.GetField("handle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (handleField?.GetValue(windowObj) is IntPtr fieldHandle)
        {
            hwnd = fieldHandle;
            return hwnd != IntPtr.Zero;
        }

        return false;
    }

    private static object? GetFieldValue(Type type, object instance, string name)
    {
        var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return field?.GetValue(instance);
    }

    [DllImport("shell32.dll", PreserveSig = true)]
    private static extern int Shell_NotifyIconGetRect(ref NOTIFYICONIDENTIFIER identifier, out NativeMethods.RECT iconLocation);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONIDENTIFIER
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public Guid guidItem;
    }
}
