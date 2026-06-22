using System.Drawing.Drawing2D;

namespace WinPicker;

public sealed class MonitorMapForm : Form
{
    private const int MarginSize = 14;
    private const int HeaderHeight = 38;
    private const int FooterHeight = 44;
    private const int Gap = 10;
    private const int MonitorLabelBand = 36;
    private const int ListRowHeight = 30;
    private const int ListHeaderHeight = 30;

    private readonly AppSettings _settings;
    private readonly Logger _logger;
    private readonly WindowEnumerator _enumerator;
    private readonly WindowMover _mover;
    private readonly WindowHighlighter _highlighter;
    private readonly WindowMoveHistory _history;
    private readonly WindowThumbnailCache _thumbnailCache;
    private readonly Image? _headerLogo;
    private readonly List<MappedWindow> _mappedWindows = new();
    private readonly List<MappedMonitor> _mappedMonitors = new();
    private readonly List<MappedListItem> _mappedListItems = new();

    private List<WindowInfo> _windows = new();
    private Rectangle _virtualBounds = Rectangle.Empty;
    private Rectangle _mapBounds = Rectangle.Empty;
    private Rectangle _listBounds = Rectangle.Empty;
    private float _scale = 1.0f;
    private int _selectedIndex = -1;
    private IntPtr _hoveredHandle = IntPtr.Zero;
    private int _listScrollOffset;
    private string _statusMessage = UiText.InitialStatus;
    private bool _contextMenuOpen;
    private bool _firstPaintCompleted;

    public MonitorMapForm(AppSettings settings, Logger logger, WindowMoveHistory history)
    {
        _settings = settings;
        _logger = logger;
        _history = history;
        _enumerator = new WindowEnumerator(settings, logger);
        _mover = new WindowMover(settings, logger, history);
        _highlighter = new WindowHighlighter(settings, logger);
        _thumbnailCache = new WindowThumbnailCache(logger);
        _headerLogo = BrandingImageLoader.LoadImage("CyfomixHeader.png");

        Text = "WinPicker";
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
        Opacity = 0.01;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        UpdateStyles();

        // Do not close immediately on Deactivate by default.
        // Moving the mouse over a mapped window can show a topmost highlight overlay,
        // and that may cause this form to lose activation on some Windows 11 environments.
        // v0.3 keeps the picker open while the cursor is inside the mini window.
        Deactivate += (_, _) =>
        {
            if (_contextMenuOpen)
                return;

            BeginInvoke(new Action(() =>
            {
                if (IsDisposed || _contextMenuOpen)
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
        g.DrawString("WinPicker", titleFont, textBrush, titleX, titleY);

        var titleWidth = (int)Math.Ceiling(g.MeasureString("WinPicker", titleFont).Width);
        var instructionX = titleX + titleWidth + 12;
        var instructionWidth = Math.Max(1, ClientSize.Width - instructionX - MarginSize);
        var instructionRect = new Rectangle(instructionX, 12, instructionWidth, 20);
        TextRenderer.DrawText(g, UiText.HeaderInstruction, Font, instructionRect, Color.FromArgb(170, 170, 170), TextFormatFlags.EndEllipsis | TextFormatFlags.Left | TextFormatFlags.Top);
    }

    private void DrawFooter(Graphics g)
    {
        using var brush = new SolidBrush(Color.FromArgb(170, 170, 170));
        using var statusBrush = new SolidBrush(Color.FromArgb(255, 210, 90));
        var y1 = ClientSize.Height - FooterHeight + 5;
        var y2 = y1 + 18;

        g.DrawString(UiText.FooterHelp, Font, brush, MarginSize, y1);

        var message = Shorten(_statusMessage, Math.Max(20, (ClientSize.Width - MarginSize * 2) / 7));
        g.DrawString(message, Font, statusBrush, MarginSize, y2);
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
        // v0.12: place labels above monitors in the upper row and below monitors in the lower row.
        // This keeps labels away from thumbnails while following the visual grid more naturally.
        var rowPivot = GetMonitorRowPivot();
        var monitorCenterY = screenRect.Top + (screenRect.Height / 2f);
        var isUpperRow = monitorCenterY <= rowPivot;

        var labelY = isUpperRow
            ? screenRect.Top - MonitorLabelBand + 4
            : screenRect.Bottom + 4;

        // Fallback for very small picker sizes: draw just inside the monitor.
        if (labelY < HeaderHeight + 2 || labelY + 30 > ClientSize.Height - FooterHeight)
            labelY = isUpperRow ? screenRect.Top + 6 : Math.Max(screenRect.Top + 6, screenRect.Bottom - 32);

        var label = UiText.Monitor(index + 1);
        if (isTarget)
            label += $"  [{UiText.TargetTag}]";
        if (isPrimary)
            label += $"  [{UiText.PrimaryTag}]";

        var labelRect = new Rectangle(screenRect.Left + 4, labelY, Math.Max(1, screenRect.Width - 8), 16);
        var deviceRect = new Rectangle(screenRect.Left + 4, labelY + 16, Math.Max(1, screenRect.Width - 8), 16);

        TextRenderer.DrawText(g, label, Font, labelRect, isTarget ? Color.FromArgb(255, 210, 90) : Color.FromArgb(235, 235, 235), TextFormatFlags.EndEllipsis | TextFormatFlags.Left | TextFormatFlags.Top);
        TextRenderer.DrawText(g, screen.DeviceName, Font, deviceRect, Color.FromArgb(170, 170, 170), TextFormatFlags.EndEllipsis | TextFormatFlags.Left | TextFormatFlags.Top);
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

        for (var i = 0; i < _windows.Count; i++)
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
                TextRenderer.DrawText(g, label, Font, textRect, textColor, TextFormatFlags.EndEllipsis | TextFormatFlags.Left | TextFormatFlags.Top);
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
        if (!_settings.ShowWindowList || _listBounds == Rectangle.Empty)
            return;

        using var listFont = CreateWindowListFont();
        using var headerFont = new Font(listFont.FontFamily, Math.Min(22f, listFont.Size + 1.0f), FontStyle.Regular);
        var rowHeight = GetListRowHeight();
        var headerHeight = GetListHeaderHeight();

        using var panelBrush = new SolidBrush(Color.FromArgb(30, 30, 30));
        using var borderPen = new Pen(Color.FromArgb(80, 80, 80));
        using var headerBrush = new SolidBrush(Color.FromArgb(45, 45, 45));
        using var textBrush = new SolidBrush(Color.FromArgb(235, 235, 235));
        using var subBrush = new SolidBrush(Color.FromArgb(170, 170, 170));

        g.FillRectangle(panelBrush, _listBounds);
        g.DrawRectangle(borderPen, _listBounds);
        g.FillRectangle(headerBrush, new Rectangle(_listBounds.Left, _listBounds.Top, _listBounds.Width, headerHeight));
        g.DrawString(UiText.WindowsListHeader(_windows.Count), headerFont, textBrush, _listBounds.Left + 8, _listBounds.Top + Math.Max(4, (headerHeight - headerFont.Height) / 2));

        var visibleRows = Math.Max(1, (_listBounds.Height - headerHeight - 4) / rowHeight);
        EnsureListSelectionVisible(visibleRows);
        _listScrollOffset = Math.Clamp(_listScrollOffset, 0, Math.Max(0, _windows.Count - visibleRows));

        var y = _listBounds.Top + headerHeight + 2;
        for (var row = 0; row < visibleRows; row++)
        {
            var index = _listScrollOffset + row;
            if (index < 0 || index >= _windows.Count)
                break;

            var window = _windows[index];
            var rowRect = new Rectangle(_listBounds.Left + 4, y, _listBounds.Width - 8, rowHeight - 2);
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
            var titleSource = window.IsMinimized ? $"{UiText.MinimizedTag} {window.Title}" : window.Title;
            var title = Shorten(titleSource, Math.Max(16, (int)((_listBounds.Width - 24) / charWidth)));
            var processSource = window.IsMinimized ? $"{window.ProcessName} / {UiText.MinimizedStatus}" : window.ProcessName;
            var process = Shorten(processSource, Math.Max(16, (int)((_listBounds.Width - 24) / Math.Max(6f, processFont.Size * 0.72f))));
            TextRenderer.DrawText(g, title, listFont, new Rectangle(rowRect.Left + 6, rowRect.Top + 2, rowRect.Width - 12, titleHeight), window.IsMinimized ? Color.FromArgb(210, 220, 255) : Color.FromArgb(245, 245, 245), TextFormatFlags.EndEllipsis | TextFormatFlags.Left | TextFormatFlags.Top);
            TextRenderer.DrawText(g, process, processFont, new Rectangle(rowRect.Left + 6, rowRect.Top + titleHeight + 1, rowRect.Width - 12, processHeight), Color.FromArgb(170, 170, 170), TextFormatFlags.EndEllipsis | TextFormatFlags.Left | TextFormatFlags.Top);

            _mappedListItems.Add(new MappedListItem(index, window, rowRect));
            y += rowHeight;
        }

        if (_windows.Count > visibleRows)
        {
            var scrollText = $"{_listScrollOffset + 1}-{Math.Min(_windows.Count, _listScrollOffset + visibleRows)} / {_windows.Count}";
            var size = TextRenderer.MeasureText(scrollText, listFont);
            TextRenderer.DrawText(g, scrollText, listFont, new Point(_listBounds.Right - size.Width - 8, _listBounds.Top + Math.Max(4, (headerHeight - listFont.Height) / 2)), Color.FromArgb(160, 160, 160));
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
            ShowMonitorMenuIfNeeded(e.Location);
            return;
        }

        if (e.Button != MouseButtons.Left)
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

        var visibleRows = Math.Max(1, (_listBounds.Height - GetListHeaderHeight() - 4) / GetListRowHeight());
        var delta = e.Delta > 0 ? -3 : 3;
        _listScrollOffset = Math.Clamp(_listScrollOffset + delta, 0, Math.Max(0, _windows.Count - visibleRows));
        Invalidate();
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
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

    private bool TrySelectListItem(Point location, bool summon, bool hoverOnly = false)
    {
        if (!_settings.ShowWindowList || _listBounds == Rectangle.Empty)
            return false;

        foreach (var item in _mappedListItems)
        {
            if (!item.Bounds.Contains(location))
                continue;

            _selectedIndex = item.Index;
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
        if (index >= 0)
            _selectedIndex = index;

        if (_hoveredHandle != window.Handle)
        {
            _hoveredHandle = window.Handle;
            _highlighter.Highlight(window);
        }

        _statusMessage = status;
        EnsureListSelectionVisible();
        Invalidate();
        BeginInvoke(new Action(BringPickerToFront));
    }

    private void ShowMonitorMenuIfNeeded(Point location)
    {
        var monitor = _mappedMonitors.LastOrDefault(m => m.MapBounds.Contains(location));
        if (monitor is null)
            return;

        var menu = CreateDarkMenu();
        var targetItem = new ToolStripMenuItem(UiText.SetThisMonitorAsTarget);
        targetItem.Click += (_, _) => SetTargetMonitor(monitor);
        var infoItem = new ToolStripMenuItem(UiText.TargetInfo(monitor.Index + 1, monitor.Screen.DeviceName))
        {
            Enabled = false
        };

        menu.Items.Add(targetItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(infoItem);
        menu.Closed += (_, _) =>
        {
            _contextMenuOpen = false;
            BeginInvoke(new Action(Activate));
        };

        _contextMenuOpen = true;
        menu.Show(this, location);
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
            Renderer = new ToolStripProfessionalRenderer(new DarkColorTable())
        };
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
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

        var visibleRows = Math.Max(1, (_listBounds.Height - GetListHeaderHeight() - 4) / GetListRowHeight());
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

    private sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Color.FromArgb(55, 55, 55);
        public override Color MenuItemBorder => Color.FromArgb(90, 90, 90);
        public override Color ToolStripDropDownBackground => Color.FromArgb(34, 34, 34);
        public override Color ImageMarginGradientBegin => Color.FromArgb(34, 34, 34);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(34, 34, 34);
        public override Color ImageMarginGradientEnd => Color.FromArgb(34, 34, 34);
        public override Color SeparatorDark => Color.FromArgb(75, 75, 75);
        public override Color SeparatorLight => Color.FromArgb(75, 75, 75);
    }
}
