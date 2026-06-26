using System.Runtime.InteropServices;

namespace WinPicker;

public sealed class TrayApplicationContext : ApplicationContext
{
    private const int ShowHotkeyId = 0x5750;    // WP
    private const int RestoreHotkeyId = 0x5751; // WQ
    private const int ScreenshotHotkeyId = 0x5752; // WR

    private readonly Logger _logger;
    private readonly AppSettings _settings;
    private readonly NotifyIcon _notifyIcon;
    private readonly HotKeyWindow _hotKeyWindow;
    private readonly ModifierChordMouseMover _modifierChordMouseMover;
    private readonly WindowMoveHistory _history = new();
    private readonly WindowMover _mover;
    private MonitorMapForm? _mapForm;

    public TrayApplicationContext(AppSettings settings, Logger logger)
    {
        _settings = settings;
        _logger = logger;
        _mover = new WindowMover(settings, logger, _history);

        _notifyIcon = new NotifyIcon
        {
            Text = "WinPicker",
            Icon = IconLoader.LoadAppIcon(),
            Visible = true,
            ContextMenuStrip = BuildContextMenu()
        };

        _notifyIcon.MouseClick += OnTrayMouseClick;

        _hotKeyWindow = new HotKeyWindow(OnHotKey, _logger);
        _hotKeyWindow.CreateHandle(new CreateParams());
        RegisterHotKeys();
        _modifierChordMouseMover = new ModifierChordMouseMover(
            MoveCursorToTrayFromWinAltChord,
            MoveCursorToTrayFromAltDoubleTap,
            ShowMapFormFromAltTripleTap,
            ShowMapFormFromRightAltSpace,
            RestoreLastMoveFromRightAltZ,
            _logger);

        _logger.Info("WinPicker started.");
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = CreateDarkMenu();

        var showItem = new ToolStripMenuItem(UiText.Show, null, (_, _) => ShowMapForm(Cursor.Position));
        var restoreItem = new ToolStripMenuItem(UiText.RestoreLastMove, null, (_, _) => RestoreLastMove());
        var settingsItem = new ToolStripMenuItem(UiText.Settings, null, (_, _) => OpenSettings());
        var aboutItem = new ToolStripMenuItem(UiText.About, null, (_, _) => ShowAbout());
        var openLogsItem = new ToolStripMenuItem(UiText.OpenLogsFolder, null, (_, _) => OpenLogsFolder());
        var exitItem = new ToolStripMenuItem(UiText.Exit, null, (_, _) => ExitThread());

        menu.Items.Add(showItem);
        menu.Items.Add(restoreItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(settingsItem);
        menu.Items.Add(aboutItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(openLogsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        return menu;
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

    private void OnTrayMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            ShowMapForm(Cursor.Position);
    }

    private void OnHotKey(int hotkeyId)
    {
        if (hotkeyId == ShowHotkeyId)
        {
            var anchor = ResolveHotkeyAnchorPoint();
            ShowMapForm(anchor);
            return;
        }

        if (hotkeyId == RestoreHotkeyId)
        {
            RestoreLastMove();
            return;
        }

        if (hotkeyId == ScreenshotHotkeyId)
        {
            CaptureScreenshotFromHotkey();
            return;
        }
    }

    private async void CaptureScreenshotFromHotkey()
    {
        try
        {
            if (_mapForm is { IsDisposed: false })
            {
                _mapForm.Hide();
                _mapForm.Close();
                _mapForm = null;
                await Task.Delay(220);
            }

            var path = ScreenshotCaptureService.CaptureAllScreens(_logger);
            _notifyIcon.ShowBalloonTip(1800, "WinPicker", UiText.ScreenshotSaved(path), ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            _logger.Error("Screenshot hotkey failed.", ex);
            _notifyIcon.ShowBalloonTip(1800, "WinPicker", UiText.ScreenshotFailed, ToolTipIcon.Warning);
        }
    }

    private void ShowMapForm(Point fallbackAnchor)
    {
        try
        {
            if (_mapForm is { IsDisposed: false })
            {
                _mapForm.Close();
                _mapForm = null;
                return;
            }

            _mapForm = new MonitorMapForm(_settings, _logger, _history, OpenSettings);
            _mapForm.FormClosed += (_, _) => _mapForm = null;

            var placement = _settings.PopupPlacementMode ?? "Cursor";
            if (placement.Equals("Primary", StringComparison.OrdinalIgnoreCase) && Screen.PrimaryScreen is not null)
            {
                _mapForm.ShowCenteredOnScreen(Screen.PrimaryScreen);
            }
            else if (placement.Equals("Target", StringComparison.OrdinalIgnoreCase))
            {
                _mapForm.ShowCenteredOnScreen(_mover.ResolveTargetScreenForDisplay());
            }
            else if (placement.Equals("SpecificMonitor", StringComparison.OrdinalIgnoreCase) && TryFindScreenByDevice(_settings.PopupMonitorDeviceName, out var screen))
            {
                _mapForm.ShowCenteredOnScreen(screen);
            }
            else
            {
                _mapForm.ShowAt(fallbackAnchor);
            }

            _mapForm.BringPickerToFront();
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to show MonitorMapForm.", ex);
        }
    }


    private Point ResolveHotkeyAnchorPoint() => MoveCursorToTrayAnchor("Hotkey");

    private void MoveCursorToTrayFromWinAltChord()
    {
        // Win+Alt alone only moves the cursor near the task tray.
        // If Space is pressed after that, the existing Win+Alt+Space hotkey still opens the picker.
        _ = MoveCursorToTrayAnchor("Win+Alt chord");
    }

    private void MoveCursorToTrayFromAltDoubleTap()
    {
        // Move the cursor when either Left Alt or Right Alt is tapped twice quickly.
        // This avoids moving the cursor on an ordinary single Alt press.
        _ = MoveCursorToTrayAnchor("Alt double-tap");
    }

    private void ShowMapFormFromAltTripleTap()
    {
        var anchor = MoveCursorToTrayAnchor("Alt triple-tap");
        ShowMapForm(anchor);
    }

    private void ShowMapFormFromRightAltSpace()
    {
        var anchor = MoveCursorToTrayAnchor("RightAlt+Space");
        ShowMapForm(anchor);
    }

    private void RestoreLastMoveFromRightAltZ()
    {
        _ = MoveCursorToTrayAnchor("RightAlt+Z");
        RestoreLastMove();
    }

    private Point MoveCursorToTrayAnchor(string source)
    {
        if (!_settings.MoveCursorToTrayOnHotkey)
            return Cursor.Position;

        try
        {
            if (_settings.PreferExactTrayIconPosition && NotifyIconLocator.TryGetIconCenter(_notifyIcon, _logger, out var iconCenter))
            {
                Cursor.Position = iconCenter;
                _logger.Info($"{source} moved cursor to WinPicker tray icon. x={iconCenter.X} y={iconCenter.Y}");
                return iconCenter;
            }

            var screen = Screen.PrimaryScreen ?? Screen.FromPoint(Cursor.Position);
            var anchor = EstimateTrayAnchor(screen);
            Cursor.Position = anchor;
            _logger.Info($"{source} moved cursor to estimated tray area. x={anchor.X} y={anchor.Y}");
            return anchor;
        }
        catch (Exception ex)
        {
            _logger.Error($"{source} failed to move cursor to tray anchor.", ex);
            return Cursor.Position;
        }
    }

    private static Point EstimateTrayAnchor(Screen screen)
    {
        // Windows does not expose the exact WinForms NotifyIcon rectangle in a reliable public API.
        // Use the taskbar/tray side inferred from Bounds vs WorkingArea, then choose a point near
        // the notification area. This keeps the picker opening in a stable, easy-to-find location.
        var bounds = screen.Bounds;
        var work = screen.WorkingArea;

        if (work.Bottom < bounds.Bottom) // taskbar at bottom
            return new Point(bounds.Right - 28, work.Bottom + Math.Max(8, (bounds.Bottom - work.Bottom) / 2));

        if (work.Top > bounds.Top) // taskbar at top
            return new Point(bounds.Right - 28, bounds.Top + Math.Max(8, (work.Top - bounds.Top) / 2));

        if (work.Right < bounds.Right) // taskbar at right
            return new Point(work.Right + Math.Max(8, (bounds.Right - work.Right) / 2), bounds.Bottom - 28);

        if (work.Left > bounds.Left) // taskbar at left
            return new Point(bounds.Left + Math.Max(8, (work.Left - bounds.Left) / 2), bounds.Bottom - 28);

        return new Point(bounds.Right - 28, bounds.Bottom - 28);
    }

    private static bool TryFindScreenByDevice(string? deviceName, out Screen screen)
    {
        foreach (var s in Screen.AllScreens)
        {
            if (string.Equals(s.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
            {
                screen = s;
                return true;
            }
        }

        screen = Screen.FromPoint(Cursor.Position);
        return false;
    }

    private void RestoreLastMove()
    {
        try
        {
            if (!_mover.RestoreLast())
            {
                _notifyIcon.ShowBalloonTip(
                    1800,
                    "WinPicker",
                    UiText.NoRestoreHistory,
                    ToolTipIcon.Info);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("RestoreLastMove failed.", ex);
        }
    }

    private void OpenSettings()
    {
        try
        {
            using var form = new SettingsForm(_settings, _logger);
            if (form.ShowDialog() == DialogResult.OK)
            {
                RegisterHotKeys();
                _notifyIcon.ShowBalloonTip(1600, "WinPicker", UiText.SettingsSaved, ToolTipIcon.Info);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to open settings.", ex);
        }
    }

    private void ShowAbout()
    {
        using var about = new AboutForm(_settings);
        about.ShowDialog();
    }

    private void OpenLogsFolder()
    {
        try
        {
            var path = AppPaths.LogsDirectory;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to open logs folder.", ex);
        }
    }

    private void RegisterHotKeys()
    {
        try
        {
            NativeMethods.UnregisterHotKey(_hotKeyWindow.Handle, ShowHotkeyId);
            NativeMethods.UnregisterHotKey(_hotKeyWindow.Handle, RestoreHotkeyId);
            NativeMethods.UnregisterHotKey(_hotKeyWindow.Handle, ScreenshotHotkeyId);

            RegisterOneHotKey(ShowHotkeyId, _settings.Hotkey, UiText.DisplayHotkeyLabel);
            RegisterOneHotKey(RestoreHotkeyId, _settings.RestoreHotkey, UiText.RestoreHotkeyLabel);
            RegisterOneHotKey(ScreenshotHotkeyId, "Win+Alt+P", UiText.ScreenshotHotkeyLabel);
        }
        catch (Exception ex)
        {
            _logger.Error("RegisterHotKeys failed.", ex);
        }
    }

    private void RegisterOneHotKey(int id, string hotkeyText, string label)
    {
        if (!HotkeyParser.TryParse(hotkeyText, out var definition, out var error))
        {
            _logger.Warn($"Invalid {label} hotkey: {hotkeyText}. {error}");
            _notifyIcon.ShowBalloonTip(3000, "WinPicker", UiText.InvalidHotkeyBalloon(label, hotkeyText), ToolTipIcon.Warning);
            return;
        }

        var ok = NativeMethods.RegisterHotKey(_hotKeyWindow.Handle, id, definition.Modifiers, definition.KeyCode);
        if (!ok)
        {
            var win32Error = Marshal.GetLastWin32Error();
            _logger.Warn($"RegisterHotKey failed. label={label} hotkey={definition.NormalizedText} Win32Error={win32Error}");
            _notifyIcon.ShowBalloonTip(
                3000,
                "WinPicker",
                UiText.HotkeyRegistrationFailed(label, definition.NormalizedText),
                ToolTipIcon.Warning);
        }
        else
        {
            _logger.Info($"Registered hotkey: {label} = {definition.NormalizedText}");
        }
    }

    protected override void ExitThreadCore()
    {
        try
        {
            _modifierChordMouseMover.Dispose();
            NativeMethods.UnregisterHotKey(_hotKeyWindow.Handle, ShowHotkeyId);
            NativeMethods.UnregisterHotKey(_hotKeyWindow.Handle, RestoreHotkeyId);
            NativeMethods.UnregisterHotKey(_hotKeyWindow.Handle, ScreenshotHotkeyId);
            _hotKeyWindow.DestroyHandle();
            _mapForm?.Close();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _logger.Info("WinPicker exited.");
        }
        catch (Exception ex)
        {
            _logger.Error("ExitThreadCore failed.", ex);
        }
        finally
        {
            base.ExitThreadCore();
        }
    }

    private sealed class HotKeyWindow : NativeWindow
    {
        private readonly Action<int> _onHotKey;
        private readonly Logger _logger;

        public HotKeyWindow(Action<int> onHotKey, Logger logger)
        {
            _onHotKey = onHotKey;
            _logger = logger;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY)
            {
                var id = m.WParam.ToInt32();
                _logger.Info($"Hotkey pressed. id={id}");
                _onHotKey(id);
                return;
            }

            base.WndProc(ref m);
        }
    }

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
