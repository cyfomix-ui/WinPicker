using System.Drawing.Drawing2D;

namespace WinPicker;

public sealed class MonitorMapForm : Form
{
    private const int MarginSize = 14;
    private const int HeaderHeight = 38;
    private const int FooterHeight = 44;
    private const int Gap = 10;
    private const int MonitorLabelBand = 42;
    private const int ListRowHeight = 30;
    private const int ListHeaderHeight = 30;

    private readonly AppSettings _settings;
    private readonly Logger _logger;
    private readonly WindowEnumerator _enumerator;
    private readonly WindowMover _mover;
    private readonly WindowHighlighter _highlighter;
    private readonly WindowMoveHistory _history;
    private readonly WindowMinimizeHistory _minimizeHistory;
    private readonly MonitorScreenSaverManager _monitorScreenSaverManager;
    private readonly WindowThumbnailCache _thumbnailCache;
    private readonly GeometrySnapshotService _geometrySnapshots;
    private readonly Action? _openSettingsAction;
    private readonly ToolTip _iconToolTip = new();
    private readonly PickerOutsideClickCloser? _outsideClickCloser;
    private readonly Image? _headerLogo;
    private readonly List<MappedWindow> _mappedWindows = new();
    private readonly List<MappedMonitor> _mappedMonitors = new();
    private readonly List<MappedListItem> _mappedListItems = new();

    private List<WindowInfo> _windows = new();
    private Rectangle _virtualBounds = Rectangle.Empty;
    private Rectangle _mapBounds = Rectangle.Empty;
    private Rectangle _listBounds = Rectangle.Empty;
    private Rectangle _saveGeometryButtonBounds = Rectangle.Empty;
    private Rectangle _restoreGeometryButtonBounds = Rectangle.Empty;
    private Rectangle _captureButtonBounds = Rectangle.Empty;
    private Rectangle _settingsButtonBounds = Rectangle.Empty;
    private float _scale = 1.0f;
    private int _selectedIndex = -1;
    private IntPtr _hoveredHandle = IntPtr.Zero;
    private int _listScrollOffset;
    private string _statusMessage = UiText.InitialStatus;
    private bool _contextMenuOpen;
    private bool _modalDialogOpen;
    private bool _firstPaintCompleted;
    private DateTime _lastBringToFrontFromHoverUtc = DateTime.MinValue;
    private string _currentIconToolTipText = string.Empty;

    public MonitorMapForm(AppSettings settings, Logger logger, WindowMoveHistory history, WindowMinimizeHistory minimizeHistory, MonitorScreenSaverManager monitorScreenSaverManager, Action? openSettingsAction = null)
    {
        _settings = settings;
        _logger = logger;
        _history = history;
        _minimizeHistory = minimizeHistory;
        _monitorScreenSaverManager = monitorScreenSaverManager;
        _enumerator = new WindowEnumerator(settings, logger);
        _mover = new WindowMover(settings, logger, history);
        _highlighter = new WindowHighlighter(settings, logger);
        _thumbnailCache = new WindowThumbnailCache(logger);
        _geometrySnapshots = new GeometrySnapshotService(logger);
        _openSettingsAction = openSettingsAction;

        if (_settings.ClosePopupOnOutsideClick)
            _outsideClickCloser = new PickerOutsideClickCloser(this, () => _contextMenuOpen, _logger);

        _headerLogo = BrandingImageLoader.LoadImage("CyfomixHeader.png");

        Text = UiText.AppTitleWithVersion;
        Icon = IconLoader.LoadAppIcon();
        StartPosition = FormStartPosition.Manual;
        Width = Math.Max(settings.PopupWidth, 640);
        Height = Math.Max(settings.PopupHeight, 380);
        MinimumSize = new Size(620, 380);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(24, 24, 24);
        ForeColor = Color.FromArgb(235, 235, 235);
        Font = new Font("Segoe UI", 9f);
        _iconToolTip.BackColor = Color.FromArgb(34, 34, 34);
        _iconToolTip.ForeColor = Color.FromArgb(245, 245, 245);
        _iconToolTip.InitialDelay = 250;
        _iconToolTip.ReshowDelay = 100;
        _iconToolTip.AutoPopDelay = 3500;
        Opacity = 0.01;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        UpdateStyles();

        // Do not close immediately on Deactivate by default.
        // Moving the mouse over a mapped window can show a topmost highlight overlay,
        // and that may cause this form to lose activation on some Windows 11 environments.
        // v0.3 keeps the picker open while the cursor is inside the mini window.
        Deactivate += (_, _) =>
        {
            if (_contextMenuOpen || _modalDialogOpen)
                return;

            BeginInvoke(new Action(() =>
            {
                if (IsDisposed || _contextMenuOpen || _modalDialogOpen)
                    return;

                var mouseInside = Bounds.Contains(Cursor.Position);
                if (_settings.ClosePopupOnDeactivate && !mouseInside)
                {
                    Close();
                    return;
                }

                // v0.6: while the mini picker is visible, keep it keyboard-controllable.
                // The hover border overlay and other windows can temporarily steal activation.
                if (_settings.KeepPickerFocused)
                    BringPickerToFront();
            }));
        };
        Load += (_, _) => RefreshWindows();
        MouseClick += OnMouseClick;
        MouseMove += OnMouseMove;
        MouseDoubleClick += OnMouseDoubleClick;
        MouseWheel += OnMouseWheel;
        MouseLeave += (_, _) => ClearHover();
        KeyDown += OnKeyDown;
    }

    public void ShowAt(Point anchor)
    {
        var workingArea = Screen.FromPoint(anchor).WorkingArea;
        var x = anchor.X >= workingArea.Left + workingArea.Width / 2
            ? anchor.X - Width + 18
            : anchor.X - 18;
        var y = anchor.Y >= workingArea.Top + workingArea.Height / 2
            ? anchor.Y - Height - 18
            : anchor.Y + 18;

        x = Math.Clamp(x, workingArea.Left, Math.Max(workingArea.Left, workingArea.Right - Width));
        y = Math.Clamp(y, workingArea.Top, Math.Max(workingArea.Top, workingArea.Bottom - Height));

        Location = new Point(x, y);
        Show();
        BringPickerToFront();
    }

    public void ShowCenteredOnScreen(Screen screen)
    {
        var workingArea = screen.WorkingArea;
        var x = workingArea.Left + Math.Max(0, (workingArea.Width - Width) / 2);
        var y = workingArea.Top + Math.Max(0, (workingArea.Height - Height) / 2);
        Location = new Point(x, y);
        Show();
        BringPickerToFront();
    }

    public void BringPickerToFront()
    {
        if (IsDisposed)
            return;

        try
        {
            TopMost = true;
            NativeMethods.SetWindowPos(
                Handle,
                NativeMethods.HWND_TOPMOST,
                Left,
                Top,
                Width,
                Height,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_SHOWWINDOW);
            BringToFront();
            Activate();
            Focus();
        }
        catch
        {
            // ignored: this method is best-effort focus recovery.
        }
    }

    private void RefreshWindows()
    {
        try
        {
            ClearHover();
            _thumbnailCache.Clear();
            _windows = _enumerator.Enumerate();
            _selectedIndex = _windows.Count > 0 ? 0 : -1;
            _logger.Info($"Enumerated windows: {_windows.Count}");
            Invalidate();
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to refresh window map.", ex);
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        TryEnableDarkTitleBar();
        BringPickerToFront();

        // Keep the initial WinForms white/default paint hidden until our first dark paint completes.
        BeginInvoke(new Action(() =>
        {
            if (!_firstPaintCompleted && !IsDisposed)
                Invalidate();
        }));
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var brush = new SolidBrush(BackColor);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        TryEnableDarkTitleBar();
    }

    private void TryEnableDarkTitleBar()
    {
        try
        {
            var useDark = 1;
            NativeMethods.DwmSetWindowAttribute(
                Handle,
                NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref useDark,
                sizeof(int));
        }
        catch
        {
            // Older Windows builds may ignore this. The client area is still painted dark.
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        ClearHover();
        _highlighter.Dispose();
        _thumbnailCache.Dispose();
        _iconToolTip.Dispose();
        _outsideClickCloser?.Dispose();
        _headerLogo?.Dispose();
        base.OnFormClosed(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        DrawHeader(g);
        BuildMapGeometry();
        DrawMonitors(g);
        DrawWindows(g);
        DrawWindowList(g);
        DrawFooter(g);

        if (!_firstPaintCompleted)
        {
            _firstPaintCompleted = true;
            BeginInvoke(new Action(() =>
            {
                if (!IsDisposed)
                    Opacity = 1.0;
            }));
        }
    }

    private void DrawHeader(Graphics g)
    {
        using var titleFont = new Font(Font.FontFamily, 11f, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.FromArgb(242, 242, 242));
        using var subBrush = new SolidBrush(Color.FromArgb(170, 170, 170));

        const int logoSize = 28;
        var logoX = MarginSize;
        var logoY = 5;
        if (_headerLogo is not null)
        {
            g.DrawImage(_headerLogo, new Rectangle(logoX, logoY, logoSize, logoSize));
        }

        var titleX = _headerLogo is not null ? logoX + logoSize + 8 : MarginSize;
        var titleY = 10;
        var titleText = UiText.AppTitleWithVersion;
        g.DrawString(titleText, titleFont, textBrush, titleX, titleY);

        var titleWidth = (int)Math.Ceiling(g.MeasureString(titleText, titleFont).Width);
        var instructionX = titleX + titleWidth + 12;
        var instructionWidth = Math.Max(1, ClientSize.Width - instructionX - MarginSize);
        var instructionRect = new Rectangle(instructionX, 12, instructionWidth, 20);
        TextRenderer.DrawText(g, UiText.HeaderInstruction, Font, instructionRect, Color.FromArgb(170, 170, 170), TextFormatFlags.EndEllipsis | TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.PreserveGraphicsClipping);
    }

    private void DrawFooter(Graphics g)
    {
        using var statusFont = new Font("Segoe UI", 8.0f, FontStyle.Regular);
        using var helpFont = new Font("Segoe UI", 7.6f, FontStyle.Regular);

        var yStatus = ClientSize.Height - FooterHeight + 4;
        var yHelp = yStatus + 18;

        var footerRight = ClientSize.Width - MarginSize;
        if (_settings.ShowWindowList && _listBounds != Rectangle.Empty)
            footerRight = Math.Max(MarginSize + 80, _listBounds.Left - Gap);

        var footerWidth = Math.Max(1, footerRight - MarginSize);
        var statusRect = new Rectangle(MarginSize, yStatus, footerWidth, 17);
        var helpRect = new Rectangle(MarginSize, yHelp, footerWidth, 17);

        // v0.29: status/window name is shown above in yellow, key help below in smaller gray text.
        TextRenderer.DrawText(
            g,
            _statusMessage,
            statusFont,
            statusRect,
            Color.FromArgb(255, 210, 90),
            TextFormatFlags.EndEllipsis | TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding | TextFormatFlags.PreserveGraphicsClipping);

        TextRenderer.DrawText(
            g,
            UiText.FooterHelp,
            helpFont,
            helpRect,
            Color.FromArgb(170, 170, 170),
            TextFormatFlags.EndEllipsis | TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding | TextFormatFlags.PreserveGraphicsClipping);
    }

    private void BuildMapGeometry()
    {
        var screens = Screen.AllScreens;
        if (screens.Length == 0)
        {
            _virtualBounds = Rectangle.Empty;
            _mapBounds = Rectangle.Empty;
            _scale = 1f;
            return;
        }

        var left = screens.Min(s => s.Bounds.Left);
        var top = screens.Min(s => s.Bounds.Top);
        var right = screens.Max(s => s.Bounds.Right);
        var bottom = screens.Max(s => s.Bounds.Bottom);
        _virtualBounds = Rectangle.FromLTRB(left, top, right, bottom);

        var available = new Rectangle(
            MarginSize,
            HeaderHeight + MarginSize,
            ClientSize.Width - MarginSize * 2,
            ClientSize.Height - HeaderHeight - FooterHeight - MarginSize * 2);

        // v0.9: keep monitor labels outside the monitor rectangles.
        // The top/bottom label bands prevent the labels from covering window thumbnails.
        if (available.Height > 240)
        {
            available = new Rectangle(
                available.Left,
                available.Top + MonitorLabelBand,
                available.Width,
                Math.Max(1, available.Height - MonitorLabelBand * 2));
        }

        _listBounds = Rectangle.Empty;
        if (_settings.ShowWindowList && available.Width > 620)
        {
            var listWidth = Math.Clamp(_settings.WindowListWidth, 190, Math.Max(190, available.Width / 2));
            _listBounds = new Rectangle(available.Right - listWidth, available.Top, listWidth, available.Height);
            available = new Rectangle(available.Left, available.Top, Math.Max(1, available.Width - listWidth - Gap), available.Height);
        }

        if (_virtualBounds.Width <= 0 || _virtualBounds.Height <= 0 || available.Width <= 0 || available.Height <= 0)
        {
            _mapBounds = available;
            _scale = 1f;
            return;
        }

        var scaleX = (available.Width - Gap) / (float)_virtualBounds.Width;
        var scaleY = (available.Height - Gap) / (float)_virtualBounds.Height;
        _scale = Math.Max(0.05f, Math.Min(scaleX, scaleY));

        var mapWidth = (int)Math.Round(_virtualBounds.Width * _scale);
        var mapHeight = (int)Math.Round(_virtualBounds.Height * _scale);
        var x = available.Left + (available.Width - mapWidth) / 2;
        var y = available.Top + (available.Height - mapHeight) / 2;
        _mapBounds = new Rectangle(x, y, mapWidth, mapHeight);
    }

    private void DrawMonitors(Graphics g)
    {
        _mappedMonitors.Clear();
        var screens = Screen.AllScreens;
        for (var i = 0; i < screens.Length; i++)
        {
            var screen = screens[i];
            var screenRect = ToMapRect(screen.Bounds);
            var isTarget = IsTargetScreen(i, screen);
            var isPrimary = Screen.PrimaryScreen?.DeviceName == screen.DeviceName;

            using var fillBrush = new SolidBrush(isTarget ? Color.FromArgb(38, 67, 95) : Color.FromArgb(42, 42, 42));
            using var borderPen = new Pen(isTarget ? Color.FromArgb(80, 180, 255) : Color.FromArgb(95, 95, 95), isTarget ? 2.4f : 1.1f);
            using var gridPen = new Pen(Color.FromArgb(54, 54, 54));
            using var labelBrush = new SolidBrush(Color.FromArgb(235, 235, 235));
            using var subBrush = new SolidBrush(Color.FromArgb(170, 170, 170));
            using var targetBrush = new SolidBrush(Color.FromArgb(255, 210, 90));

            g.FillRectangle(fillBrush, screenRect);
            g.DrawRectangle(borderPen, screenRect);

            for (var x = screenRect.Left + 80; x < screenRect.Right; x += 80)
                g.DrawLine(gridPen, x, screenRect.Top, x, screenRect.Bottom);
            for (var y = screenRect.Top + 60; y < screenRect.Bottom; y += 60)
                g.DrawLine(gridPen, screenRect.Left, y, screenRect.Right, y);

            DrawMonitorLabel(g, screen, screenRect, i, isTarget, isPrimary, labelBrush, subBrush, targetBrush);

            _mappedMonitors.Add(new MappedMonitor(i, screen, screenRect));
        }
    }

    private void DrawMonitorLabel(
        Graphics g,
        Screen screen,
        Rectangle screenRect,
        int index,
        bool isTarget,
        bool isPrimary,
        Brush labelBrush,
        Brush subBrush,
        Brush targetBrush)
    {
        // v0.20: use smaller dedicated monitor-label fonts.
        // At high DPI / many-monitor layouts, the normal form font can overlap the monitor map.
        using var monitorFont = new Font(Font.FontFamily, 7.5f, FontStyle.Regular);
        using var deviceFont = new Font(Font.FontFamily, 7.0f, FontStyle.Regular);

        var labelLineHeight = Math.Max(12, TextRenderer.MeasureText("Hg", monitorFont).Height - 3);
        var deviceLineHeight = Math.Max(11, TextRenderer.MeasureText("Hg", deviceFont).Height - 4);
        var labelBlockHeight = labelLineHeight + deviceLineHeight + 2;

        // v0.12: place labels above monitors in the upper row and below monitors in the lower row.
        // This keeps labels away from thumbnails while following the visual grid more naturally.
        var rowPivot = GetMonitorRowPivot();
        var monitorCenterY = screenRect.Top + (screenRect.Height / 2f);
        var isUpperRow = monitorCenterY <= rowPivot;

        var labelY = isUpperRow
            ? screenRect.Top - labelBlockHeight - 3
            : screenRect.Bottom + 3;

        // Fallback for very small picker sizes: draw just inside the monitor.
        if (labelY < HeaderHeight + 2 || labelY + labelBlockHeight > ClientSize.Height - FooterHeight)
            labelY = isUpperRow ? screenRect.Top + 4 : Math.Max(screenRect.Top + 4, screenRect.Bottom - labelBlockHeight - 4);

        var label = UiText.Monitor(index + 1);
        if (isTarget)
            label += $"  [{UiText.TargetTag}]";
        if (isPrimary)
            label += $"  [{UiText.PrimaryTag}]";

        var labelRect = new Rectangle(screenRect.Left + 3, labelY, Math.Max(1, screenRect.Width - 6), labelLineHeight);
        var deviceRect = new Rectangle(screenRect.Left + 3, labelY + labelLineHeight + 1, Math.Max(1, screenRect.Width - 6), deviceLineHeight);

        TextRenderer.DrawText(
            g,
            label,
            monitorFont,
            labelRect,
            isTarget ? Color.FromArgb(255, 210, 90) : Color.FromArgb(235, 235, 235),
            TextFormatFlags.EndEllipsis | TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding);

        TextRenderer.DrawText(
            g,
            screen.DeviceName,
            deviceFont,
            deviceRect,
            Color.FromArgb(170, 170, 170),
            TextFormatFlags.EndEllipsis | TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding);
    }

    private float GetMonitorRowPivot()
    {
        var screens = Screen.AllScreens;
        if (screens.Length <= 1)
            return float.MaxValue;

        var centers = screens
            .Select(s =>
            {
                var r = ToMapRect(s.Bounds);
                return r.Top + (r.Height / 2f);
            })
            .OrderBy(v => v)
            .ToArray();

        if (centers.Length == 0)
            return float.MaxValue;

        // For two-row layouts, use the largest vertical gap between monitor centers.
        var pivot = centers[0];
        var maxGap = float.MinValue;
        for (var i = 0; i < centers.Length - 1; i++)
        {
            var gap = centers[i + 1] - centers[i];
            if (gap > maxGap)
            {
                maxGap = gap;
                pivot = (centers[i] + centers[i + 1]) / 2f;
            }
        }

        return maxGap <= 24f ? centers.Average() : pivot;
    }

    private void DrawWindows(Graphics g)
    {
        _mappedWindows.Clear();

        // _windows is in real Z-order from front to back.
        // Draw back-to-front so the visible/front window is painted last.
        for (var i = _windows.Count - 1; i >= 0; i--)
        {
            var window = _windows[i];
            var rect = ToMapRect(window.Bounds);

            if (rect.Width < 20)
                rect.Width = 20;
            if (rect.Height < 18)
                rect.Height = 18;

            var selected = i == _selectedIndex;
            var hovered = window.Handle == _hoveredHandle;

            using var fillBrush = new SolidBrush(hovered ? Color.FromArgb(255, 210, 90) : selected ? Color.FromArgb(92, 76, 38) : Color.FromArgb(55, 82, 118));
            using var borderPen = new Pen(hovered ? Color.FromArgb(255, 245, 160) : selected ? Color.FromArgb(255, 180, 60) : Color.FromArgb(95, 145, 205), hovered ? 2.6f : selected ? 2.0f : 1.0f);

            var drawThumbnail = _settings.ShowWindowList && _settings.ShowWindowThumbnails && rect.Width >= 56 && rect.Height >= 40;
            if (drawThumbnail)
                DrawWindowThumbnail(g, window, rect, selected, hovered);
            else
                g.FillRectangle(fillBrush, rect);

            g.DrawRectangle(borderPen, rect);

            if (_settings.ShowWindowTitlesInMap || !_settings.ShowWindowList || !drawThumbnail)
            {
                var label = Shorten(window.IsMinimized ? $"{UiText.MinimizedTag} {window.Title}" : window.Title, Math.Max(8, rect.Width / 7));
                var labelHeight = Math.Min(24, Math.Max(16, rect.Height / 3));
                var textRect = new Rectangle(rect.Left + 4, rect.Top + 3, Math.Max(1, rect.Width - 8), Math.Max(1, labelHeight));
                var textBack = new Rectangle(rect.Left + 1, rect.Top + 1, Math.Max(1, rect.Width - 2), Math.Max(1, labelHeight + 4));
                using var textBackBrush = new SolidBrush(Color.FromArgb(drawThumbnail ? 175 : 0, 20, 20, 20));
                if (drawThumbnail)
                    g.FillRectangle(textBackBrush, textBack);

                var textColor = hovered ? Color.FromArgb(30, 30, 30) : Color.FromArgb(245, 245, 245);
                TextRenderer.DrawText(g, label, Font, textRect, textColor, TextFormatFlags.EndEllipsis | TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.PreserveGraphicsClipping);
            }

            _mappedWindows.Add(new MappedWindow(window, rect));
        }
    }

    private void DrawWindowThumbnail(Graphics g, WindowInfo window, Rectangle rect, bool selected, bool hovered)
    {
        using var fallbackBrush = new SolidBrush(hovered ? Color.FromArgb(255, 210, 90) : selected ? Color.FromArgb(92, 76, 38) : Color.FromArgb(55, 82, 118));
        g.FillRectangle(fallbackBrush, rect);

        var thumb = _thumbnailCache.GetThumbnail(window);
        if (thumb is null)
            return;

        var inner = Rectangle.Inflate(rect, -2, -2);
        if (inner.Width <= 0 || inner.Height <= 0)
            return;

        var drawRect = FitRectangle(thumb.Size, inner);
        g.DrawImage(thumb, drawRect);

        if (selected || hovered)
        {
            using var overlay = new SolidBrush(Color.FromArgb(hovered ? 60 : 40, 255, 210, 90));
            g.FillRectangle(overlay, rect);
        }
    }

    private static Rectangle FitRectangle(Size source, Rectangle dest)
    {
        if (source.Width <= 0 || source.Height <= 0)
            return dest;

        var scale = Math.Min(dest.Width / (float)source.Width, dest.Height / (float)source.Height);
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        return new Rectangle(
            dest.Left + (dest.Width - width) / 2,
            dest.Top + (dest.Height - height) / 2,
            width,
            height);
    }

    private void DrawWindowList(Graphics g)
    {
        _mappedListItems.Clear();
        _saveGeometryButtonBounds = Rectangle.Empty;
        _restoreGeometryButtonBounds = Rectangle.Empty;
        _captureButtonBounds = Rectangle.Empty;
        _settingsButtonBounds = Rectangle.Empty;

        if (!_settings.ShowWindowList || _listBounds == Rectangle.Empty)
            return;

        using var listFont = CreateWindowListFont();
        using var headerFont = new Font(listFont.FontFamily, Math.Min(22f, listFont.Size + 1.0f), FontStyle.Regular);
        var rowHeight = GetListRowHeight();
        var headerHeight = GetListHeaderHeight();

        using var panelBrush = new SolidBrush(Color.FromArgb(30, 30, 30));
        using var borderPen = new Pen(Color.FromArgb(80, 80, 80));
        using var headerBrush = new SolidBrush(Color.FromArgb(72, 60, 104));
        using var textBrush = new SolidBrush(Color.FromArgb(235, 235, 235));

        g.FillRectangle(panelBrush, _listBounds);
        g.DrawRectangle(borderPen, _listBounds);
        g.FillRectangle(headerBrush, new Rectangle(_listBounds.Left, _listBounds.Top, _listBounds.Width, headerHeight));

        // v0.23: place action icons above the list frame, not inside the list header.
        // This makes the list header cleaner and allows larger round icon buttons.
        var buttonSize = 31;
        var buttonGap = 8;
        var buttonY = Math.Max(HeaderHeight + 4, _listBounds.Top - buttonSize - 7);
        _settingsButtonBounds = new Rectangle(_listBounds.Right - buttonGap - buttonSize, buttonY, buttonSize, buttonSize);
        _captureButtonBounds = new Rectangle(_settingsButtonBounds.Left - buttonGap - buttonSize, buttonY, buttonSize, buttonSize);
        _restoreGeometryButtonBounds = new Rectangle(_captureButtonBounds.Left - buttonGap - buttonSize, buttonY, buttonSize, buttonSize);
        _saveGeometryButtonBounds = new Rectangle(_restoreGeometryButtonBounds.Left - buttonGap - buttonSize, buttonY, buttonSize, buttonSize);

        var headerTextRect = new Rectangle(
            _listBounds.Left + 8,
            _listBounds.Top + Math.Max(1, (headerHeight - headerFont.Height) / 2),
            Math.Max(1, _listBounds.Width - 16),
            headerFont.Height + 2);

        TextRenderer.DrawText(g, UiText.WindowsListHeader(_windows.Count), headerFont, headerTextRect, Color.FromArgb(245, 245, 245), TextFormatFlags.EndEllipsis | TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

        DrawRoundIconButton(g, _saveGeometryButtonBounds, "save");
        DrawRoundIconButton(g, _restoreGeometryButtonBounds, "restore");
        DrawRoundIconButton(g, _captureButtonBounds, "camera");
        DrawRoundIconButton(g, _settingsButtonBounds, "settings");

        var visibleRows = Math.Max(1, (_listBounds.Height - headerHeight - 36) / rowHeight);
        EnsureListSelectionVisible(visibleRows);
        _listScrollOffset = Math.Clamp(_listScrollOffset, 0, Math.Max(0, _windows.Count - visibleRows));

        var contentClip = new Rectangle(
            _listBounds.Left + 1,
            _listBounds.Top + headerHeight + 1,
            Math.Max(1, _listBounds.Width - 2),
            Math.Max(1, _listBounds.Height - headerHeight - 30));

        var oldClip = g.Save();
        g.SetClip(contentClip);

        var y = _listBounds.Top + headerHeight + 3;
        for (var row = 0; row < visibleRows; row++)
        {
            var index = _listScrollOffset + row;
            if (index < 0 || index >= _windows.Count)
                break;

            var window = _windows[index];
            var rowRect = new Rectangle(_listBounds.Left + 4, y, _listBounds.Width - 8, rowHeight - 3);
            if (rowRect.Bottom > contentClip.Bottom)
                break;

            var selected = index == _selectedIndex;
            var hovered = window.Handle == _hoveredHandle;

            using var rowBrush = new SolidBrush(hovered ? Color.FromArgb(84, 76, 38) : selected ? Color.FromArgb(55, 86, 125) : Color.FromArgb(38, 38, 38));
            using var rowPen = new Pen(hovered ? Color.FromArgb(255, 210, 90) : selected ? Color.FromArgb(100, 170, 245) : Color.FromArgb(58, 58, 58));
            g.FillRectangle(rowBrush, rowRect);
            g.DrawRectangle(rowPen, rowRect);

            var processFontSize = Math.Max(7f, listFont.Size - 1.0f);
            using var processFont = new Font(listFont.FontFamily, processFontSize, FontStyle.Regular);

            var titleHeight = Math.Max(14, (int)Math.Ceiling(listFont.Height * 1.05));
            var processHeight = Math.Max(12, rowRect.Height - titleHeight - 4);
            var charWidth = Math.Max(6.5f, listFont.Size * 0.72f);
            var itemKey = GetListItemKeyLabel(index);
            var keyWidth = Math.Max(22, TextRenderer.MeasureText(itemKey, listFont).Width + 8);
            var keyRect = new Rectangle(rowRect.Left + 6, rowRect.Top + 2, keyWidth, titleHeight);
            var titleRect = new Rectangle(rowRect.Left + 6 + keyWidth, rowRect.Top + 2, Math.Max(1, rowRect.Width - 18 - keyWidth), titleHeight);
            var processRect = new Rectangle(rowRect.Left + 6 + keyWidth, rowRect.Top + titleHeight + 1, Math.Max(1, rowRect.Width - 18 - keyWidth), processHeight);

            var elevatedPrefix = window.IsElevated ? $"{UiText.ElevatedTag} " : "";
            var titleSource = elevatedPrefix + (window.IsMinimized ? $"{UiText.MinimizedTag} {window.Title}" : window.Title);
            var title = Shorten(titleSource, Math.Max(16, (int)((_listBounds.Width - 24 - keyWidth) / charWidth)));
            var processSource = window.IsMinimized ? $"{window.ProcessName} / {UiText.MinimizedStatus}" : window.ProcessName;
            var process = Shorten(processSource, Math.Max(16, (int)((_listBounds.Width - 24 - keyWidth) / Math.Max(6f, processFont.Size * 0.72f))));
            var titleColor = window.IsElevated ? Color.FromArgb(255, 222, 145) : window.IsMinimized ? Color.FromArgb(210, 220, 255) : Color.FromArgb(245, 245, 245);

            TextRenderer.DrawText(g, itemKey, listFont, keyRect, Color.FromArgb(255, 210, 90), TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.PreserveGraphicsClipping);
            TextRenderer.DrawText(g, title, listFont, titleRect, titleColor, TextFormatFlags.EndEllipsis | TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.PreserveGraphicsClipping);
            TextRenderer.DrawText(g, process, processFont, processRect, Color.FromArgb(170, 170, 170), TextFormatFlags.EndEllipsis | TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.PreserveGraphicsClipping);

            _mappedListItems.Add(new MappedListItem(index, window, rowRect));
            y += rowHeight;
        }

        g.Restore(oldClip);

        // v0.24: TextRenderer/GDI text can sometimes leak a partial row at the very bottom,
        // especially with high DPI and fractional row calculations. Paint a small safety band.
        var cleanupBand = new Rectangle(
            _listBounds.Left + 1,
            Math.Max(_listBounds.Top + headerHeight + 1, _listBounds.Bottom - 24),
            Math.Max(1, _listBounds.Width - 2),
            23);
        using (var cleanupBrush = new SolidBrush(Color.FromArgb(30, 30, 30)))
        {
            g.FillRectangle(cleanupBrush, cleanupBand);
        }
        using (var borderPen2 = new Pen(Color.FromArgb(80, 80, 80)))
        {
            g.DrawRectangle(borderPen2, _listBounds);
        }
    }

    private void DrawRoundIconButton(Graphics g, Rectangle bounds, string kind)
    {
        if (bounds == Rectangle.Empty)
            return;

        using var circleBrush = new SolidBrush(Color.FromArgb(54, 54, 58));
        using var borderPen = new Pen(Color.FromArgb(130, 130, 138), 1.1f);
        using var iconPen = new Pen(Color.FromArgb(232, 232, 238), 1.7f);
        using var iconBrush = new SolidBrush(Color.FromArgb(232, 232, 238));

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.FillEllipse(circleBrush, bounds);
        g.DrawEllipse(borderPen, bounds);

        var cx = bounds.Left + bounds.Width / 2;
        var cy = bounds.Top + bounds.Height / 2;

        if (kind == "save")
        {
            var r = new Rectangle(bounds.Left + 9, bounds.Top + 8, bounds.Width - 18, bounds.Height - 17);
            g.DrawRectangle(iconPen, r);
            g.DrawLine(iconPen, r.Left + 2, r.Top + 5, r.Right - 2, r.Top + 5);
            g.FillRectangle(iconBrush, r.Left + 3, r.Bottom - 5, r.Width - 6, 4);
        }
        else if (kind == "restore")
        {
            var r = new Rectangle(bounds.Left + 8, bounds.Top + 8, bounds.Width - 16, bounds.Height - 16);
            g.DrawArc(iconPen, r, 35, 280);
            var p1 = new Point(r.Left + 2, cy);
            var p2 = new Point(r.Left + 8, cy - 6);
            var p3 = new Point(r.Left + 8, cy + 6);
            g.FillPolygon(iconBrush, new[] { p1, p2, p3 });
        }
        else if (kind == "camera")
        {
            var body = new Rectangle(bounds.Left + 7, bounds.Top + 11, bounds.Width - 14, bounds.Height - 15);
            g.DrawRectangle(iconPen, body);
            g.FillRectangle(iconBrush, body.Left + 4, body.Top - 4, 8, 4);
            g.DrawEllipse(iconPen, cx - 5, cy - 2, 10, 10);
        }
        else if (kind == "settings")
        {
            // Simple gear: outer teeth + inner circle.
            using var gearPen = new Pen(Color.FromArgb(232, 232, 238), 1.55f);
            var outer = new Rectangle(cx - 7, cy - 7, 14, 14);
            var inner = new Rectangle(cx - 3, cy - 3, 6, 6);

            for (var i = 0; i < 8; i++)
            {
                var angle = Math.PI * 2 * i / 8.0;
                var x1 = cx + (int)Math.Round(Math.Cos(angle) * 8);
                var y1 = cy + (int)Math.Round(Math.Sin(angle) * 8);
                var x2 = cx + (int)Math.Round(Math.Cos(angle) * 11);
                var y2 = cy + (int)Math.Round(Math.Sin(angle) * 11);
                g.DrawLine(gearPen, x1, y1, x2, y2);
            }

            g.DrawEllipse(gearPen, outer);
            g.DrawEllipse(gearPen, inner);
        }
    }

    private Font CreateWindowListFont()
    {
        var size = Math.Clamp(_settings.WindowListFontSize, 7.0f, 22.0f);
        return new Font("Segoe UI", size, FontStyle.Regular);
    }

    private int GetListRowHeight()
    {
        var size = Math.Clamp(_settings.WindowListFontSize, 7.0f, 22.0f);
        return Math.Max(24, (int)Math.Round(size * 3.25f));
    }

    private int GetListHeaderHeight()
    {
        var size = Math.Clamp(_settings.WindowListFontSize, 7.0f, 22.0f);
        return Math.Max(28, (int)Math.Round(size * 2.95f));
    }

    private void AdjustWindowListFontSize(int wheelDelta)
    {
        var step = wheelDelta > 0 ? 0.5f : -0.5f;
        var oldSize = _settings.WindowListFontSize;
        var newSize = Math.Clamp((float)Math.Round((oldSize + step) * 2f) / 2f, 7.0f, 22.0f);
        if (Math.Abs(newSize - oldSize) < 0.01f)
            return;

        _settings.WindowListFontSize = newSize;
        SettingsService.Save(_settings, _logger);
        _statusMessage = UiText.WindowListFontSizeSaved(newSize);
        Invalidate();
        BringPickerToFront();
    }

    private bool IsTargetScreen(int index, Screen screen)
    {
        if (!string.IsNullOrWhiteSpace(_settings.TargetMonitorDeviceName))
            return string.Equals(screen.DeviceName, _settings.TargetMonitorDeviceName, StringComparison.OrdinalIgnoreCase);

        if (_settings.UsePrimaryScreen && Screen.PrimaryScreen is not null)
            return string.Equals(screen.DeviceName, Screen.PrimaryScreen.DeviceName, StringComparison.OrdinalIgnoreCase);

        var screens = Screen.AllScreens;
        return index == Math.Clamp(_settings.MainMonitorIndex, 0, Math.Max(0, screens.Length - 1));
    }

    private Rectangle ToMapRect(Rectangle source)
    {
        if (_virtualBounds == Rectangle.Empty)
            return Rectangle.Empty;

        var x = _mapBounds.Left + (int)Math.Round((source.Left - _virtualBounds.Left) * _scale);
        var y = _mapBounds.Top + (int)Math.Round((source.Top - _virtualBounds.Top) * _scale);
        var width = (int)Math.Round(source.Width * _scale);
        var height = (int)Math.Round(source.Height * _scale);
        return new Rectangle(x, y, Math.Max(1, width), Math.Max(1, height));
    }

    private void OnMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            ShowContextMenuIfNeeded(e.Location);
            return;
        }

        if (e.Button != MouseButtons.Left)
            return;

        if (TryHandleListButtonClick(e.Location))
            return;

        if (TrySelectListItem(e.Location, summon: false))
            return;

        for (var i = _mappedWindows.Count - 1; i >= 0; i--)
        {
            if (!_mappedWindows[i].MapBounds.Contains(e.Location))
                continue;

            _selectedIndex = _windows.IndexOf(_mappedWindows[i].Window);
            SummonSelected();
            return;
        }
    }

    private void OnMouseDoubleClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            TrySelectListItem(e.Location, summon: true);
    }

    private void OnMouseWheel(object? sender, MouseEventArgs e)
    {
        if (!_settings.ShowWindowList || !_listBounds.Contains(e.Location))
            return;

        if ((ModifierKeys & Keys.Control) == Keys.Control)
        {
            AdjustWindowListFontSize(e.Delta);
            return;
        }

        var visibleRows = Math.Max(1, (_listBounds.Height - GetListHeaderHeight() - 36) / GetListRowHeight());
        var delta = e.Delta > 0 ? -3 : 3;
        _listScrollOffset = Math.Clamp(_listScrollOffset + delta, 0, Math.Max(0, _windows.Count - visibleRows));
        Invalidate();
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (UpdateIconToolTip(e.Location))
            return;

        if (TrySelectListItem(e.Location, summon: false, hoverOnly: true))
            return;

        for (var i = _mappedWindows.Count - 1; i >= 0; i--)
        {
            var mapped = _mappedWindows[i];
            if (!mapped.MapBounds.Contains(e.Location))
                continue;

            SelectAndHighlight(mapped.Window, Shorten(mapped.Window.Title, 72));
            return;
        }

        if (_hoveredHandle != IntPtr.Zero)
        {
            ClearHover();
            Invalidate();
        }
    }

    private bool UpdateIconToolTip(Point location)
    {
        var text = string.Empty;

        if (_saveGeometryButtonBounds.Contains(location))
            text = UiText.TooltipSaveLayout;
        else if (_restoreGeometryButtonBounds.Contains(location))
            text = UiText.TooltipRestoreLayout;
        else if (_captureButtonBounds.Contains(location))
            text = UiText.TooltipScreenshot;
        else if (_settingsButtonBounds.Contains(location))
            text = UiText.TooltipSettings;

        if (!string.Equals(_currentIconToolTipText, text, StringComparison.Ordinal))
        {
            _currentIconToolTipText = text;
            _iconToolTip.SetToolTip(this, text);
        }

        return false;
    }

    private bool TryHandleListButtonClick(Point location)
    {
        if (_saveGeometryButtonBounds.Contains(location))
        {
            SaveGeometrySnapshot();
            return true;
        }

        if (_restoreGeometryButtonBounds.Contains(location))
        {
            ShowGeometryRestoreMenu(_restoreGeometryButtonBounds);
            return true;
        }

        if (_captureButtonBounds.Contains(location))
        {
            CaptureScreenshot();
            return true;
        }

        if (_settingsButtonBounds.Contains(location))
        {
            OpenSettingsFromPicker();
            return true;
        }

        return false;
    }

    private void OpenSettingsFromPicker()
    {
        try
        {
            // v0.28: close the picker itself before opening Settings.
            // Keeping the picker alive/topmost can steal focus from the settings dialog on some systems.
            var openSettings = _openSettingsAction;

            TopMost = false;
            Enabled = false;
            Opacity = 0.0;
            Hide();

            BeginInvoke(new Action(() =>
            {
                try
                {
                    Close();
                    openSettings?.Invoke();
                }
                catch (Exception ex)
                {
                    _logger.Error("Failed to open settings from picker after closing picker.", ex);
                }
            }));
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to open settings from picker.", ex);
            try
            {
                Close();
            }
            catch
            {
                // Ignore close failures during fallback.
            }
        }
    }

    private void SaveGeometrySnapshot()
    {
        try
        {
            // v0.24: save immediately with a timestamp name.
            // The modal dialog could lose input focus because the picker is TopMost and aggressively keeps focus.
            var snapshot = _geometrySnapshots.Save(null, _windows);
            _statusMessage = UiText.GeometrySaved(snapshot.Name);
            Invalidate();
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to save geometry snapshot.", ex);
        }
    }

    private void ShowGeometryRestoreMenu(Rectangle buttonBounds)
    {
        try
        {
            var snapshots = _geometrySnapshots.LoadSnapshots();
            if (snapshots.Count == 0)
            {
                _statusMessage = UiText.NoGeometrySnapshots;
                Invalidate();
                return;
            }

            var menu = CreateDarkMenu();
            foreach (var snapshot in snapshots.OrderByDescending(s => s.CreatedAt).Take(8))
            {
                var label = $"{snapshot.Name}  ({snapshot.CreatedAt:MM/dd HH:mm})";
                var item = CreateDarkMenuItem(label);

                var restoreWindowsItem = CreateDarkMenuItem(UiText.GeometryRestoreWindows);
                restoreWindowsItem.Click += (_, _) =>
                {
                    _geometrySnapshots.RestoreWindows(snapshot, _enumerator);
                    _statusMessage = UiText.GeometryWindowsRestored(snapshot.Name);
                    RefreshWindows();
                };

                var restoreIconsItem = CreateDarkMenuItem(UiText.GeometryRestoreIcons);
                restoreIconsItem.Click += (_, _) =>
                {
                    _geometrySnapshots.RestoreDesktopIcons(snapshot);
                    _statusMessage = UiText.GeometryIconsRestored(snapshot.Name);
                    Invalidate();
                };

                item.DropDownItems.Add(restoreWindowsItem);
                item.DropDownItems.Add(restoreIconsItem);
                menu.Items.Add(item);
            }

            menu.Show(this, new Point(buttonBounds.Left, buttonBounds.Bottom + 3));
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to show geometry restore menu.", ex);
        }
    }

    private async void CaptureScreenshot()
    {
        try
        {
            // Hide the picker before capture so the saved image is the desktop after WinPicker closes.
            Hide();
            Opacity = 0.0;
            await Task.Delay(220);

            var path = ScreenshotCaptureService.CaptureAllScreens(_logger);
            _logger.Info(UiText.ScreenshotSaved(path));

            Close();
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to capture screenshot.", ex);
            Close();
        }
    }

    private bool TrySelectListItem(Point location, bool summon, bool hoverOnly = false)
    {
        if (!_settings.ShowWindowList || _listBounds == Rectangle.Empty)
            return false;

        foreach (var item in _mappedListItems)
        {
            if (!item.Bounds.Contains(location))
                continue;

            SelectAndHighlight(item.Window, Shorten(item.Window.Title, 72));
            if (summon)
                SummonSelected();
            else if (!hoverOnly)
                _statusMessage = UiText.SelectedStatus(Shorten(item.Window.Title, 64));
            return true;
        }

        return _listBounds.Contains(location);
    }

    private void SelectAndHighlight(WindowInfo window, string status)
    {
        var index = _windows.IndexOf(window);
        var selectionChanged = index >= 0 && _selectedIndex != index;
        var hoverChanged = _hoveredHandle != window.Handle;
        var statusChanged = !string.Equals(_statusMessage, status, StringComparison.Ordinal);

        // MouseMove fires very frequently. Avoid repainting / SetWindowPos / overlay work
        // when the cursor is still on the same mapped window or list row.
        if (!selectionChanged && !hoverChanged && !statusChanged)
            return;

        if (index >= 0)
            _selectedIndex = index;

        if (hoverChanged)
        {
            _hoveredHandle = window.Handle;
            _highlighter.Highlight(window);
        }

        if (statusChanged)
            _statusMessage = status;

        if (selectionChanged)
            EnsureListSelectionVisible();

        Invalidate();

        if (_settings.KeepPickerFocused && hoverChanged)
        {
            var now = DateTime.UtcNow;
            if (now - _lastBringToFrontFromHoverUtc > TimeSpan.FromMilliseconds(350))
            {
                _lastBringToFrontFromHoverUtc = now;
                BeginInvoke(new Action(BringPickerToFront));
            }
        }
    }

    private void ShowContextMenuIfNeeded(Point location)
    {
        var monitor = _mappedMonitors.LastOrDefault(m => m.MapBounds.Contains(location));
        if (monitor is null)
            return;

        var mappedWindow = FindMappedWindowAt(location);
        var menu = CreateDarkMenu();

        if (mappedWindow is not null)
        {
            var canRestoreWindow = mappedWindow.Window.IsMinimized || _minimizeHistory.HasWindow(mappedWindow.Window.Handle);
            var appItem = CreateDarkMenuItem(canRestoreWindow ? UiText.RestoreThisApp : UiText.MinimizeThisApp);
            appItem.Click += (_, _) =>
            {
                if (canRestoreWindow)
                    RestoreWindow(mappedWindow.Window);
                else
                    MinimizeWindow(mappedWindow.Window);
            };
            menu.Items.Add(appItem);
            menu.Items.Add(new ToolStripSeparator());
        }

        var targetItem = CreateDarkMenuItem(UiText.SetThisMonitorAsTarget);
        targetItem.Click += (_, _) => SetTargetMonitor(monitor);

        var monitorKey = MonitorScreenSaverManager.GetScreenKey(monitor.Screen);
        var canRestoreMonitor = _minimizeHistory.HasMonitor(monitorKey);
        var minimizeMonitorAppsItem = CreateDarkMenuItem(canRestoreMonitor ? UiText.RestoreAppsOnThisMonitor : UiText.MinimizeAppsOnThisMonitor);
        minimizeMonitorAppsItem.Click += (_, _) =>
        {
            if (canRestoreMonitor)
                RestoreWindowsOnMonitor(monitor);
            else
                MinimizeWindowsOnMonitor(monitor);
        };

        var saverMenu = CreateDarkMenuItem(UiText.SaverKindMenu);
        AddSaverKindItem(saverMenu, monitor, "Off", UiText.SaverKindOff);
        AddSaverKindItem(saverMenu, monitor, "Black", UiText.SaverKindBlack);
        AddSaverKindItem(saverMenu, monitor, "RandomText", UiText.SaverKindRandomText);
        AddSaverKindItem(saverMenu, monitor, "Lines", UiText.SaverKindLines);
        AddSaverKindItem(saverMenu, monitor, "Bubbles", UiText.SaverKindBubbles);

        var idleMenu = CreateDarkMenuItem(UiText.MonitorSaverIdleMenu);
        AddMonitorIdleMinutesItem(idleMenu, monitor, 0, UiText.MonitorSaverIdleGlobal(_settings.MonitorScreenSaverIdleMinutes));
        AddMonitorIdleMinutesItem(idleMenu, monitor, 1, UiText.MonitorSaverIdleMinutesItem(1));
        AddMonitorIdleMinutesItem(idleMenu, monitor, 3, UiText.MonitorSaverIdleMinutesItem(3));
        AddMonitorIdleMinutesItem(idleMenu, monitor, 5, UiText.MonitorSaverIdleMinutesItem(5));
        AddMonitorIdleMinutesItem(idleMenu, monitor, 10, UiText.MonitorSaverIdleMinutesItem(10));
        AddMonitorIdleMinutesItem(idleMenu, monitor, 15, UiText.MonitorSaverIdleMinutesItem(15));
        AddMonitorIdleMinutesItem(idleMenu, monitor, 30, UiText.MonitorSaverIdleMinutesItem(30));
        AddMonitorIdleMinutesItem(idleMenu, monitor, 60, UiText.MonitorSaverIdleMinutesItem(60));
        idleMenu.DropDownItems.Add(new ToolStripSeparator());
        var customIdleItem = CreateDarkMenuItem(UiText.MonitorSaverIdleCustom);
        customIdleItem.Click += (_, _) => PromptMonitorIdleMinutes(monitor);
        idleMenu.DropDownItems.Add(customIdleItem);

        var powerMenu = CreateDarkMenuItem(UiText.MonitorPowerControlMenu);
        var powerEnabledItem = CreateDarkMenuItem(UiText.MonitorPowerControlEnabled);
        powerEnabledItem.Checked = GetMonitorPowerControlEnabled(monitor);
        powerEnabledItem.Click += (_, _) => ToggleMonitorPowerControl(monitor);
        powerMenu.DropDownItems.Add(powerEnabledItem);
        var powerIp = GetMonitorPowerControlIp(monitor);
        var powerIpItem = CreateDarkMenuItem($"{UiText.MonitorPowerControlIp}  [{(string.IsNullOrWhiteSpace(powerIp) ? UiText.MonitorPowerControlIpUnset : powerIp)}]");
        powerIpItem.Click += (_, _) => PromptMonitorPowerControlIp(monitor);
        powerMenu.DropDownItems.Add(powerIpItem);

        var runEvenWhenMediaItem = CreateDarkMenuItem(UiText.SaverRunEvenWhenMediaVisible);
        runEvenWhenMediaItem.Checked = GetRunEvenWhenMediaVisible(monitor);
        runEvenWhenMediaItem.Click += (_, _) => ToggleRunEvenWhenMediaVisible(monitor);

        var currentKind = GetMonitorSaverKind(monitor);
        var infoItem = CreateDarkMenuItem(UiText.TargetInfo(monitor.Index + 1, monitor.Screen.DeviceName));
        infoItem.Enabled = false;
        var saverInfoItem = CreateDarkMenuItem(UiText.SaverKindSet(monitor.Index + 1, SaverKindDisplayName(currentKind)));
        saverInfoItem.Enabled = false;
        var idleInfoItem = CreateDarkMenuItem(UiText.MonitorSaverIdleCurrent(
            monitor.Index + 1,
            GetMonitorIdleMinutes(monitor),
            Math.Clamp(_settings.MonitorScreenSaverIdleMinutes, 1, 240)));
        idleInfoItem.Enabled = false;

        menu.Items.Add(targetItem);
        menu.Items.Add(minimizeMonitorAppsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(saverMenu);
        menu.Items.Add(idleMenu);
        menu.Items.Add(powerMenu);
        menu.Items.Add(runEvenWhenMediaItem);
        menu.Items.Add(new ToolStripSeparator());

        if (!string.IsNullOrWhiteSpace(powerIp))
        {
            var powerOnItem = CreateDarkMenuItem(UiText.ManualMonitorPowerOn);
            powerOnItem.Click += (_, _) => RequestMonitorPower(monitor, "on");
            menu.Items.Add(powerOnItem);

            var powerOffItem = CreateDarkMenuItem(UiText.ManualMonitorPowerOff);
            powerOffItem.Click += (_, _) => RequestMonitorPower(monitor, "off");
            menu.Items.Add(powerOffItem);
            menu.Items.Add(new ToolStripSeparator());
        }

        menu.Items.Add(infoItem);
        menu.Items.Add(saverInfoItem);
        menu.Items.Add(idleInfoItem);
        menu.Closed += (_, _) =>
        {
            _contextMenuOpen = false;
            BeginInvoke(new Action(Activate));
        };

        _contextMenuOpen = true;
        menu.Show(this, location);
    }

    private MappedWindow? FindMappedWindowAt(Point location)
    {
        // _mappedWindows is drawn back-to-front, so walk backwards to pick the visually topmost item.
        for (var i = _mappedWindows.Count - 1; i >= 0; i--)
        {
            if (_mappedWindows[i].MapBounds.Contains(location))
                return _mappedWindows[i];
        }

        return null;
    }

    private void MinimizeWindow(WindowInfo window)
    {
        try
        {
            ClearHover();
            var ok = NativeMethods.ShowWindow(window.Handle, NativeMethods.SW_MINIMIZE);
            if (ok)
                _minimizeHistory.RecordWindow(window.Handle);

            _statusMessage = UiText.AppMinimized(Shorten(window.Title, 48));
            _logger.Info($"Window minimized from map menu. hwnd=0x{window.Handle.ToInt64():X} title=\"{window.Title}\" ok={ok}");
            RefreshWindows();
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to minimize selected window from map menu.", ex);
        }
    }

    private void RestoreWindow(WindowInfo window)
    {
        try
        {
            ClearHover();
            var ok = NativeMethods.ShowWindow(window.Handle, NativeMethods.SW_RESTORE);
            NativeMethods.SetForegroundWindow(window.Handle);
            _minimizeHistory.RemoveWindow(window.Handle);
            _statusMessage = UiText.AppRestored(Shorten(window.Title, 48));
            _logger.Info($"Window restored from map menu. hwnd=0x{window.Handle.ToInt64():X} title=\"{window.Title}\" ok={ok}");
            RefreshWindows();
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to restore selected window from map menu.", ex);
        }
    }

    private void MinimizeWindowsOnMonitor(MappedMonitor monitor)
    {
        try
        {
            ClearHover();

            var minimizedHandles = new List<IntPtr>();
            foreach (var window in _windows.ToList())
            {
                if (window.IsMinimized)
                    continue;

                if (!WindowTouchesMonitor(window.Bounds, monitor.Screen.Bounds))
                    continue;

                if (NativeMethods.ShowWindow(window.Handle, NativeMethods.SW_MINIMIZE))
                    minimizedHandles.Add(window.Handle);
            }

            var key = MonitorScreenSaverManager.GetScreenKey(monitor.Screen);
            _minimizeHistory.RecordMonitor(key, minimizedHandles);

            _statusMessage = UiText.MonitorAppsMinimized(monitor.Index + 1, minimizedHandles.Count);
            _logger.Info($"Monitor windows minimized from map menu. monitor={monitor.Index} count={minimizedHandles.Count}");
            RefreshWindows();
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to minimize monitor windows from map menu.", ex);
        }
    }

    private void RestoreWindowsOnMonitor(MappedMonitor monitor)
    {
        try
        {
            ClearHover();

            var key = MonitorScreenSaverManager.GetScreenKey(monitor.Screen);
            var handles = _minimizeHistory.TakeMonitor(key);
            var count = 0;
            foreach (var handle in handles)
            {
                if (handle == IntPtr.Zero)
                    continue;

                if (NativeMethods.ShowWindow(handle, NativeMethods.SW_RESTORE))
                    count++;
            }

            _statusMessage = UiText.MonitorAppsRestored(monitor.Index + 1, count);
            _logger.Info($"Monitor windows restored from map menu. monitor={monitor.Index} count={count}");
            RefreshWindows();
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to restore monitor windows from map menu.", ex);
        }
    }

    private static bool WindowTouchesMonitor(Rectangle windowBounds, Rectangle monitorBounds)
    {
        var intersection = Rectangle.Intersect(windowBounds, monitorBounds);
        if (intersection.Width <= 0 || intersection.Height <= 0)
            return false;

        var windowArea = Math.Max(1.0, (double)windowBounds.Width * windowBounds.Height);
        var intersectionArea = (double)intersection.Width * intersection.Height;

        return intersectionArea / windowArea >= 0.35;
    }

    private void AddMonitorIdleMinutesItem(ToolStripMenuItem parent, MappedMonitor monitor, int minutes, string label)
    {
        var item = CreateDarkMenuItem(label);
        item.Checked = GetMonitorIdleMinutes(monitor) == minutes;
        item.Click += (_, _) => SetMonitorIdleMinutes(monitor, minutes);
        parent.DropDownItems.Add(item);
    }

    private int GetMonitorIdleMinutes(MappedMonitor monitor)
    {
        _settings.MonitorScreenSaverIdleMinutesByMonitor ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var key = MonitorScreenSaverManager.GetScreenKey(monitor.Screen);
        if (_settings.MonitorScreenSaverIdleMinutesByMonitor.TryGetValue(key, out var minutes))
            return Math.Clamp(minutes, 0, 240);

        return 0;
    }

    private void SetMonitorIdleMinutes(MappedMonitor monitor, int minutes)
    {
        try
        {
            _settings.MonitorScreenSaverIdleMinutesByMonitor ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var key = MonitorScreenSaverManager.GetScreenKey(monitor.Screen);
            _settings.MonitorScreenSaverIdleMinutesByMonitor[key] = Math.Clamp(minutes, 0, 240);
            SettingsService.Save(_settings, _logger);
            _statusMessage = UiText.MonitorSaverIdleCurrent(
                monitor.Index + 1,
                Math.Clamp(minutes, 0, 240),
                Math.Clamp(_settings.MonitorScreenSaverIdleMinutes, 1, 240));
            _logger.Info($"Monitor saver idle minutes changed. index={monitor.Index} device=\"{monitor.Screen.DeviceName}\" minutes={minutes}");
            Invalidate();
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to set monitor saver idle minutes.", ex);
        }
    }

    private void PromptMonitorIdleMinutes(MappedMonitor monitor)
    {
        try
        {
            using var dialog = new MonitorIdleMinutesDialog(
                GetMonitorIdleMinutes(monitor),
                Math.Clamp(_settings.MonitorScreenSaverIdleMinutes, 1, 240));

            if (dialog.ShowDialog(this) == DialogResult.OK)
                SetMonitorIdleMinutes(monitor, dialog.IdleMinutes);
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to open monitor saver idle minutes dialog.", ex);
        }
    }

    private bool GetMonitorPowerControlEnabled(MappedMonitor monitor)
    {
        _settings.MonitorPowerControlEnabled ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var key = MonitorScreenSaverManager.GetScreenKey(monitor.Screen);
        return _settings.MonitorPowerControlEnabled.TryGetValue(key, out var enabled) && enabled;
    }

    private string GetMonitorPowerControlIp(MappedMonitor monitor)
    {
        _settings.MonitorPowerControlIpByMonitor ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var key = MonitorScreenSaverManager.GetScreenKey(monitor.Screen);
        return _settings.MonitorPowerControlIpByMonitor.TryGetValue(key, out var ip) ? ip?.Trim() ?? string.Empty : string.Empty;
    }

    private void ToggleMonitorPowerControl(MappedMonitor monitor)
    {
        try
        {
            _settings.MonitorPowerControlEnabled ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var key = MonitorScreenSaverManager.GetScreenKey(monitor.Screen);
            var enabled = !GetMonitorPowerControlEnabled(monitor);
            _settings.MonitorPowerControlEnabled[key] = enabled;
            SettingsService.Save(_settings, _logger);
            var ip = GetMonitorPowerControlIp(monitor);
            _statusMessage = UiText.MonitorPowerControlStatus(monitor.Index + 1, enabled, string.IsNullOrWhiteSpace(ip) ? UiText.MonitorPowerControlIpUnset : ip);
            _logger.Info($"Monitor power control changed. index={monitor.Index} device=\"{monitor.Screen.DeviceName}\" enabled={enabled} ip=\"{ip}\"");
            Invalidate();
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to toggle monitor power control.", ex);
        }
    }


    private void RequestMonitorPower(MappedMonitor monitor, string state)
    {
        try
        {
            var accepted = _monitorScreenSaverManager.RequestPowerState(monitor.Screen, state);
            _statusMessage = accepted
                ? UiText.ManualMonitorPowerRequested(monitor.Index + 1, state)
                : UiText.ManualMonitorPowerUnavailable(monitor.Index + 1);
            Invalidate();
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to request monitor power state. state={state}", ex);
            _statusMessage = UiText.ManualMonitorPowerUnavailable(monitor.Index + 1);
            Invalidate();
        }
    }

    private void PromptMonitorPowerControlIp(MappedMonitor monitor)
    {
        try
        {
            using var dialog = new MonitorPowerControlDialog(GetMonitorPowerControlIp(monitor));
            _modalDialogOpen = true;
            DialogResult result;
            try
            {
                result = dialog.ShowDialog(this);
            }
            finally
            {
                _modalDialogOpen = false;
            }

            if (result != DialogResult.OK)
                return;

            _settings.MonitorPowerControlIpByMonitor ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var key = MonitorScreenSaverManager.GetScreenKey(monitor.Screen);
            _settings.MonitorPowerControlIpByMonitor[key] = dialog.DeviceIp;
            SettingsService.Save(_settings, _logger);
            var enabled = GetMonitorPowerControlEnabled(monitor);
            _statusMessage = UiText.MonitorPowerControlStatus(monitor.Index + 1, enabled, string.IsNullOrWhiteSpace(dialog.DeviceIp) ? UiText.MonitorPowerControlIpUnset : dialog.DeviceIp);
            _logger.Info($"Monitor power control IP changed. index={monitor.Index} device=\"{monitor.Screen.DeviceName}\" ip=\"{dialog.DeviceIp}\"");
            Invalidate();
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to edit monitor power control IP.", ex);
        }
    }

    private void AddSaverKindItem(ToolStripMenuItem parent, MappedMonitor monitor, string kind, string label)
    {
        var item = CreateDarkMenuItem(label);
        item.Checked = string.Equals(GetMonitorSaverKind(monitor), kind, StringComparison.OrdinalIgnoreCase);
        item.Click += (_, _) => SetMonitorSaverKind(monitor, kind);
        parent.DropDownItems.Add(item);
    }

    private string GetMonitorSaverKind(MappedMonitor monitor)
    {
        _settings.MonitorScreenSaverKinds ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var key = MonitorScreenSaverManager.GetScreenKey(monitor.Screen);
        if (_settings.MonitorScreenSaverKinds.TryGetValue(key, out var kind))
            return ScreenSaverOverlayForm.NormalizeKind(kind);

        return "Black";
    }

    private bool GetRunEvenWhenMediaVisible(MappedMonitor monitor)
    {
        _settings.MonitorScreenSaverRunEvenWhenMediaVisible ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var key = MonitorScreenSaverManager.GetScreenKey(monitor.Screen);
        return _settings.MonitorScreenSaverRunEvenWhenMediaVisible.TryGetValue(key, out var enabled) && enabled;
    }

    private void ToggleRunEvenWhenMediaVisible(MappedMonitor monitor)
    {
        try
        {
            _settings.MonitorScreenSaverRunEvenWhenMediaVisible ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var key = MonitorScreenSaverManager.GetScreenKey(monitor.Screen);
            var enabled = !GetRunEvenWhenMediaVisible(monitor);
            _settings.MonitorScreenSaverRunEvenWhenMediaVisible[key] = enabled;
            SettingsService.Save(_settings, _logger);
            _statusMessage = UiText.SaverRunEvenWhenMediaVisibleSet(monitor.Index + 1, enabled);
            _logger.Info($"Monitor saver media override changed. index={monitor.Index} device=\"{monitor.Screen.DeviceName}\" enabled={enabled}");
            Invalidate();
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to toggle monitor saver media override.", ex);
        }
    }

    private static string SaverKindDisplayName(string kind)
    {
        return ScreenSaverOverlayForm.NormalizeKind(kind) switch
        {
            "Off" => UiText.SaverKindOff,
            "RandomText" => UiText.SaverKindRandomText,
            "Lines" => UiText.SaverKindLines,
            "Bubbles" => UiText.SaverKindBubbles,
            _ => UiText.SaverKindBlack
        };
    }

    private void SetMonitorSaverKind(MappedMonitor monitor, string kind)
    {
        try
        {
            _settings.MonitorScreenSaverKinds ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var key = MonitorScreenSaverManager.GetScreenKey(monitor.Screen);
            _settings.MonitorScreenSaverKinds[key] = ScreenSaverOverlayForm.NormalizeKind(kind);
            SettingsService.Save(_settings, _logger);
            _statusMessage = UiText.SaverKindSet(monitor.Index + 1, SaverKindDisplayName(kind));
            _logger.Info($"Monitor saver kind changed. index={monitor.Index} device=\"{monitor.Screen.DeviceName}\" kind={kind}");
            Invalidate();
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to set monitor saver kind.", ex);
        }
    }

    private void SetTargetMonitor(MappedMonitor monitor)
    {
        try
        {
            _settings.UsePrimaryScreen = false;
            _settings.MainMonitorIndex = monitor.Index;
            _settings.TargetMonitorDeviceName = monitor.Screen.DeviceName;
            SettingsService.Save(_settings, _logger);
            SettingsService.SaveTargetMonitorToRegistry(_settings, _logger);
            _statusMessage = UiText.TargetSet(monitor.Index + 1);
            _logger.Info($"Target monitor changed. index={monitor.Index} device=\"{monitor.Screen.DeviceName}\"");
            Invalidate();
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to set target monitor.", ex);
        }
    }

    private static ContextMenuStrip CreateDarkMenu()
    {
        return new ContextMenuStrip
        {
            BackColor = Color.FromArgb(34, 34, 34),
            ForeColor = Color.FromArgb(235, 235, 235),
            Renderer = new DarkMenuRenderer()
        };
    }

    private static ToolStripMenuItem CreateDarkMenuItem(string text)
    {
        var item = new ToolStripMenuItem(text)
        {
            BackColor = Color.FromArgb(34, 34, 34),
            ForeColor = Color.FromArgb(235, 235, 235)
        };

        item.DropDown.BackColor = Color.FromArgb(34, 34, 34);
        item.DropDown.ForeColor = Color.FromArgb(235, 235, 235);
        item.DropDown.Renderer = new DarkMenuRenderer();
        return item;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_modalDialogOpen)
            return base.ProcessCmdKey(ref msg, keyData);

        var keyCode = keyData & Keys.KeyCode;
        var ctrl = (keyData & Keys.Control) == Keys.Control;
        var alt = (keyData & Keys.Alt) == Keys.Alt;
        var shift = (keyData & Keys.Shift) == Keys.Shift;
        if (keyCode == Keys.Escape)
        {
            Close();
            return true;
        }

        if (keyCode == Keys.F5)
        {
            RefreshWindows();
            BringPickerToFront();
            return true;
        }

        if (keyCode == Keys.Enter || keyCode == Keys.Return)
        {
            SummonSelected();
            return true;
        }

        if (keyCode == Keys.Tab)
        {
            MoveSelection(shift ? -1 : 1);
            return true;
        }

        if (keyCode is Keys.Left or Keys.Up or Keys.Right or Keys.Down)
        {
            if (ctrl && alt)
                ResizePickerWindow(keyCode, shift);
            else if (ctrl)
                MovePickerWindow(keyCode, shift);
            else if (keyCode is Keys.Left or Keys.Up)
                MoveSelection(-1);
            else
                MoveSelection(1);

            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_modalDialogOpen)
            return;

        if (e.KeyCode == Keys.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        if (e.KeyCode == Keys.F5)
        {
            RefreshWindows();
            e.Handled = true;
            return;
        }

        if (e.KeyCode == Keys.Enter)
        {
            SummonSelected();
            e.Handled = true;
            return;
        }

        if (e.KeyCode == Keys.Tab)
        {
            MoveSelection(e.Shift ? -1 : 1);
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode is Keys.Left or Keys.Up or Keys.Right or Keys.Down)
        {
            if (e.Control && e.Alt)
            {
                ResizePickerWindow(e.KeyCode, e.Shift);
            }
            else if (e.Control)
            {
                MovePickerWindow(e.KeyCode, e.Shift);
            }
            else if (e.KeyCode is Keys.Left or Keys.Up)
            {
                MoveSelection(-1);
            }
            else
            {
                MoveSelection(1);
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    public void ToggleWindowByListIndex(int index)
    {
        if (index < 0 || index >= _windows.Count)
        {
            _statusMessage = UiText.NoRestoreHistory;
            Invalidate();
            return;
        }

        _selectedIndex = index;
        var window = _windows[index];
        ClearHover();

        _mover.ToggleSummon(window, out var status);
        _statusMessage = Shorten(status, 110);

        RefreshWindows();

        if (!IsDisposed)
            BeginInvoke(new Action(BringPickerToFront));
    }

    private static bool TryGetListIndexFromHotkey(Keys keyCode, out int index)
    {
        index = -1;

        var keyValue = (int)keyCode;
        if (keyValue >= (int)Keys.D1 && keyValue <= (int)Keys.D9)
        {
            index = keyValue - (int)Keys.D1;
            return true;
        }

        if (keyValue >= (int)Keys.NumPad1 && keyValue <= (int)Keys.NumPad9)
        {
            index = keyValue - (int)Keys.NumPad1;
            return true;
        }

        if (keyValue >= (int)Keys.A && keyValue <= (int)Keys.Z)
        {
            index = 9 + keyValue - (int)Keys.A;
            return true;
        }

        return false;
    }

    private static string GetListItemKeyLabel(int index)
    {
        if (index >= 0 && index <= 8)
            return (index + 1).ToString();

        var letterIndex = index - 9;
        if (letterIndex >= 0 && letterIndex < 26)
            return ((char)('a' + letterIndex)).ToString();

        return "?";
    }

    private void MovePickerWindow(Keys keyCode, bool fast)
    {
        var step = fast ? 160 : 40;
        var dx = keyCode switch
        {
            Keys.Left => -step,
            Keys.Right => step,
            _ => 0
        };
        var dy = keyCode switch
        {
            Keys.Up => -step,
            Keys.Down => step,
            _ => 0
        };

        var newLocation = new Point(Location.X + dx, Location.Y + dy);
        var screen = Screen.FromPoint(new Point(newLocation.X + Width / 2, newLocation.Y + Height / 2));
        var area = screen.WorkingArea;

        var x = Math.Clamp(newLocation.X, area.Left, Math.Max(area.Left, area.Right - Width));
        var y = Math.Clamp(newLocation.Y, area.Top, Math.Max(area.Top, area.Bottom - Height));
        Location = new Point(x, y);
        _statusMessage = UiText.PickerMoved(fast);
        Invalidate();
        BringPickerToFront();
    }

    private void ResizePickerWindow(Keys keyCode, bool fast)
    {
        var step = fast ? 120 : 40;
        var width = Width;
        var height = Height;

        switch (keyCode)
        {
            case Keys.Left:
                width -= step;
                break;
            case Keys.Right:
                width += step;
                break;
            case Keys.Up:
                height -= step;
                break;
            case Keys.Down:
                height += step;
                break;
        }

        var minWidth = Math.Max(MinimumSize.Width, 620);
        var minHeight = Math.Max(MinimumSize.Height, 380);
        width = Math.Clamp(width, minWidth, 1800);
        height = Math.Clamp(height, minHeight, 1200);

        Size = new Size(width, height);
        KeepInsideCurrentScreen();
        PersistPickerSize();
        _statusMessage = UiText.PickerSizeSaved(Width, Height);
        Invalidate();
        BringPickerToFront();
    }

    private void KeepInsideCurrentScreen()
    {
        var screen = Screen.FromPoint(new Point(Left + Width / 2, Top + Height / 2));
        var area = screen.WorkingArea;
        var x = Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - Width));
        var y = Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - Height));
        Location = new Point(x, y);
    }

    private void PersistPickerSize()
    {
        try
        {
            _settings.PopupWidth = Width;
            _settings.PopupHeight = Height;
            SettingsService.Save(_settings, _logger);
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to save picker size.", ex);
        }
    }

    protected override void OnResizeEnd(EventArgs e)
    {
        base.OnResizeEnd(e);
        PersistPickerSize();
        Invalidate();
    }

    private void EnsureListSelectionVisible()
    {
        if (_listBounds == Rectangle.Empty)
            return;

        var visibleRows = Math.Max(1, (_listBounds.Height - GetListHeaderHeight() - 36) / GetListRowHeight());
        EnsureListSelectionVisible(visibleRows);
    }

    private void EnsureListSelectionVisible(int visibleRows)
    {
        if (_selectedIndex < 0 || _windows.Count == 0)
            return;

        if (_selectedIndex < _listScrollOffset)
            _listScrollOffset = _selectedIndex;
        else if (_selectedIndex >= _listScrollOffset + visibleRows)
            _listScrollOffset = _selectedIndex - visibleRows + 1;

        _listScrollOffset = Math.Clamp(_listScrollOffset, 0, Math.Max(0, _windows.Count - visibleRows));
    }

    private void MoveSelection(int delta)
    {
        if (_windows.Count == 0)
            return;

        _selectedIndex = _selectedIndex < 0
            ? 0
            : (_selectedIndex + delta + _windows.Count) % _windows.Count;

        var selected = _windows[_selectedIndex];
        _hoveredHandle = selected.Handle;
        _statusMessage = Shorten(selected.Title, 72);
        _highlighter.Highlight(selected);
        EnsureListSelectionVisible();
        Invalidate();
        BeginInvoke(new Action(BringPickerToFront));
    }

    private void SummonSelected()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _windows.Count)
            return;

        var window = _windows[_selectedIndex];
        ClearHover();
        _mover.Summon(window);

        if (_settings.ClosePopupAfterSummon)
            Close();
        else
            RefreshWindows();
    }

    private void ClearHover()
    {
        _hoveredHandle = IntPtr.Zero;
        _highlighter.Clear();
    }

    private static string Shorten(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value;

        return value[..Math.Max(1, maxLength - 1)] + "…";
    }

    private sealed record MappedWindow(WindowInfo Window, Rectangle MapBounds);
    private sealed record MappedMonitor(int Index, Screen Screen, Rectangle MapBounds);
    private sealed record MappedListItem(int Index, WindowInfo Window, Rectangle Bounds);

    private sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable())
        {
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Selected
                ? Color.Black
                : e.Item.Enabled ? Color.FromArgb(245, 245, 245) : Color.FromArgb(145, 145, 145);

            base.OnRenderItemText(e);
        }
    }

    private sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Color.FromArgb(160, 205, 240);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(160, 205, 240);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(160, 205, 240);
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(160, 205, 240);
        public override Color MenuItemPressedGradientMiddle => Color.FromArgb(160, 205, 240);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(160, 205, 240);
        public override Color MenuItemBorder => Color.FromArgb(80, 130, 180);
        public override Color ToolStripDropDownBackground => Color.FromArgb(34, 34, 34);
        public override Color ImageMarginGradientBegin => Color.FromArgb(34, 34, 34);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(34, 34, 34);
        public override Color ImageMarginGradientEnd => Color.FromArgb(34, 34, 34);
        public override Color SeparatorDark => Color.FromArgb(75, 75, 75);
        public override Color SeparatorLight => Color.FromArgb(75, 75, 75);
    }
}
