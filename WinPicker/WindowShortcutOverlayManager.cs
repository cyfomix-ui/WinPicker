namespace WinPicker;

internal sealed record WindowShortcutOverlayTarget(IntPtr Handle, string Label);

internal sealed class WindowShortcutOverlayManager : IDisposable
{
    private readonly Thread _thread;
    private readonly object _gate = new();
    private OverlayContext? _context;
    private WindowShortcutOverlayTarget[] _pendingTargets = Array.Empty<WindowShortcutOverlayTarget>();
    private bool _shutdownRequested;
    private bool _disposed;

    public WindowShortcutOverlayManager()
    {
        _thread = new Thread(ThreadMain)
        {
            IsBackground = true,
            Name = "WinPicker shortcut overlays"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public void Update(IEnumerable<WindowShortcutOverlayTarget> targets)
    {
        if (_disposed)
            return;
        var snapshot = targets.ToArray();
        lock (_gate)
        {
            _pendingTargets = snapshot;
            _context?.PostUpdate(snapshot);
        }
    }

    public void Clear()
    {
        if (_disposed)
            return;
        lock (_gate)
        {
            _pendingTargets = Array.Empty<WindowShortcutOverlayTarget>();
            _context?.PostClear();
        }
    }

    private void ThreadMain()
    {
        var context = new OverlayContext();
        lock (_gate)
        {
            _context = context;
            if (_pendingTargets.Length > 0)
                context.PostUpdate(_pendingTargets);
            if (_shutdownRequested)
                context.PostShutdown();
        }
        Application.Run(context);
        lock (_gate)
            _context = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        lock (_gate)
        {
            _shutdownRequested = true;
            _pendingTargets = Array.Empty<WindowShortcutOverlayTarget>();
            _context?.PostShutdown();
        }

        // Never block the map UI while the overlay message loop drains and closes its forms.
        _ = Task.Run(() =>
        {
            if (_thread.IsAlive)
                _thread.Join(TimeSpan.FromSeconds(3));
        });
    }

    private sealed class OverlayContext : ApplicationContext
    {
        private readonly Control _dispatcher = new();
        private readonly Dictionary<IntPtr, ShortcutBadgeForm> _badges = new();
        private readonly System.Windows.Forms.Timer _positionTimer;
        private WindowShortcutOverlayTarget[] _targets = Array.Empty<WindowShortcutOverlayTarget>();

        public OverlayContext()
        {
            _dispatcher.CreateControl();
            _positionTimer = new System.Windows.Forms.Timer { Interval = 350 };
            _positionTimer.Tick += (_, _) => RefreshPositions();
            _positionTimer.Start();
        }

        public void PostUpdate(WindowShortcutOverlayTarget[] targets)
        {
            if (_dispatcher.IsDisposed)
                return;
            _dispatcher.BeginInvoke(new Action(() => ApplyTargets(targets)));
        }

        public void PostClear()
        {
            if (!_dispatcher.IsDisposed)
                _dispatcher.BeginInvoke(new Action(ClearAll));
        }

        public void PostShutdown()
        {
            if (_dispatcher.IsDisposed)
                return;
            _dispatcher.BeginInvoke(new Action(() =>
            {
                ClearAll();
                _positionTimer.Stop();
                _positionTimer.Dispose();
                _dispatcher.Dispose();
                ExitThread();
            }));
        }

        private void ApplyTargets(WindowShortcutOverlayTarget[] targets)
        {
            _targets = targets;
            var handles = targets.Select(target => target.Handle).ToHashSet();
            foreach (var stale in _badges.Keys.Where(handle => !handles.Contains(handle)).ToArray())
            {
                _badges[stale].Close();
                _badges.Remove(stale);
            }

            foreach (var target in targets)
            {
                if (!_badges.TryGetValue(target.Handle, out var badge))
                {
                    badge = new ShortcutBadgeForm(target.Label);
                    _badges[target.Handle] = badge;
                    badge.Show();
                }
                else
                {
                    badge.SetLabel(target.Label);
                }
            }
            RefreshPositions();
        }

        private void RefreshPositions()
        {
            foreach (var target in _targets)
            {
                if (!_badges.TryGetValue(target.Handle, out var badge))
                    continue;

                if (!NativeMethods.IsWindow(target.Handle) || !NativeMethods.IsWindowVisible(target.Handle) || NativeMethods.IsIconic(target.Handle) ||
                    !NativeMethods.GetWindowRect(target.Handle, out var rect))
                {
                    badge.Hide();
                    continue;
                }

                var bounds = rect.ToRectangle();
                if (bounds.Width < 40 || bounds.Height < 30)
                {
                    badge.Hide();
                    continue;
                }

                var x = bounds.Right - badge.Width - 8;
                var y = bounds.Top + 8;
                NativeMethods.SetWindowPos(badge.Handle, NativeMethods.HWND_TOPMOST, x, y, badge.Width, badge.Height,
                    NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
                if (!badge.Visible)
                    badge.Show();
            }
        }

        private void ClearAll()
        {
            _targets = Array.Empty<WindowShortcutOverlayTarget>();
            foreach (var badge in _badges.Values)
                badge.Close();
            _badges.Clear();
        }
    }

    private sealed class ShortcutBadgeForm : Form
    {
        private string _label;

        public ShortcutBadgeForm(string label)
        {
            _label = label;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = Color.FromArgb(235, 145, 35);
            DoubleBuffered = true;
            UpdateBadgeSize();
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TRANSPARENT;
                return cp;
            }
        }

        public void SetLabel(string label)
        {
            if (_label == label)
                return;
            _label = label;
            UpdateBadgeSize();
            Invalidate();
        }

        private void UpdateBadgeSize()
        {
            using var font = new Font("Segoe UI", 9f, FontStyle.Bold);
            var textSize = TextRenderer.MeasureText(_label, font, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);

            Width = Math.Max(30, textSize.Width + 14);
            Height = Math.Max(24, textSize.Height + 8);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.FromArgb(235, 145, 35));
            using var font = new Font("Segoe UI", 9f, FontStyle.Bold);
            TextRenderer.DrawText(e.Graphics, _label, font, ClientRectangle, Color.Black,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTTRANSPARENT = -1;
            if (m.Msg == WM_NCHITTEST)
            {
                m.Result = new IntPtr(HTTRANSPARENT);
                return;
            }
            base.WndProc(ref m);
        }
    }
}
