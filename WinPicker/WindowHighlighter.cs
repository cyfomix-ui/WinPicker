using System.Runtime.InteropServices;

namespace WinPicker;

public sealed class WindowHighlighter : IDisposable
{
    private readonly AppSettings _settings;
    private readonly Logger _logger;
    private HighlightOverlayForm? _overlay;
    private IntPtr _currentHandle = IntPtr.Zero;

    public WindowHighlighter(AppSettings settings, Logger logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public void Highlight(WindowInfo window)
    {
        try
        {
            if (window.Handle == _currentHandle)
                return;

            _currentHandle = window.Handle;

            if (_settings.FlashWindowOnHover)
            {
                var info = new NativeMethods.FLASHWINFO
                {
                    cbSize = (uint)Marshal.SizeOf<NativeMethods.FLASHWINFO>(),
                    hwnd = window.Handle,
                    dwFlags = NativeMethods.FLASHW_CAPTION,
                    uCount = 1,
                    dwTimeout = 0
                };
                NativeMethods.FlashWindowEx(ref info);
            }

            if (_settings.ShowBorderOnHover && window.Bounds.Width > 0 && window.Bounds.Height > 0)
            {
                _overlay ??= new HighlightOverlayForm();
                _overlay.ShowAround(window.Bounds);
            }
            else
            {
                _overlay?.Hide();
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to highlight window hwnd=0x{window.Handle.ToInt64():X}", ex);
        }
    }

    public void Clear()
    {
        _currentHandle = IntPtr.Zero;
        try
        {
            _overlay?.Hide();
        }
        catch
        {
            // ignored
        }
    }

    public void Dispose()
    {
        try
        {
            _overlay?.Close();
            _overlay?.Dispose();
        }
        catch
        {
            // ignored
        }
    }

    private sealed class HighlightOverlayForm : Form
    {
        private const int BorderThickness = 6;
        private static readonly Color TransparentColor = Color.Magenta;

        public HighlightOverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = TransparentColor;
            TransparencyKey = TransparentColor;
            DoubleBuffered = true;
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= NativeMethods.WS_EX_TOOLWINDOW |
                              NativeMethods.WS_EX_TRANSPARENT |
                              NativeMethods.WS_EX_LAYERED |
                              NativeMethods.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        public void ShowAround(Rectangle targetBounds)
        {
            var bounds = Rectangle.Inflate(targetBounds, BorderThickness, BorderThickness);
            Bounds = bounds;

            if (!Visible)
                Show();

            NativeMethods.SetWindowPos(
                Handle,
                NativeMethods.HWND_TOPMOST,
                bounds.Left,
                bounds.Top,
                bounds.Width,
                bounds.Height,
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new Pen(Color.FromArgb(255, 255, 210, 40), BorderThickness);
            var rect = new Rectangle(BorderThickness / 2, BorderThickness / 2, Width - BorderThickness, Height - BorderThickness);
            e.Graphics.DrawRectangle(pen, rect);
        }
    }
}
