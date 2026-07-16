namespace WinPicker;

public sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly Logger _logger;
    private readonly TextBox _showHotkeyText = new();
    private readonly TextBox _restoreHotkeyText = new();
    private readonly ComboBox _targetMonitorCombo = new();
    private readonly ComboBox _popupModeCombo = new();
    private readonly ComboBox _popupMonitorCombo = new();
    private readonly CheckBox _closeAfterSummonCheck = new();
    private readonly CheckBox _moveCursorToTrayCheck = new();
    private readonly CheckBox _preferExactTrayIconCheck = new();
    private readonly CheckBox _keepPickerFocusedCheck = new();
    private readonly CheckBox _showWindowThumbnailsCheck = new();
    private readonly CheckBox _showWindowListCheck = new();
    private readonly CheckBox _enableAltItemHotkeysCheck = new();
    private readonly CheckBox _enableMonitorScreenSaverCheck = new();
    private readonly CheckBox _suppressMonitorScreenSaverWhenMediaVisibleCheck = new();
    private readonly CheckBox _showMonitorScreenSaverRemainingTimeCheck = new();
    private readonly TextBox _monitorScreenSaverIdleMinutesText = new();
    private readonly TextBox _tapoControlUrlText = new();
    private readonly TextBox _monitorPowerControlDelayMinutesText = new();
    private readonly CheckBox _useSummonSizeCheck = new();
    private readonly TextBox _summonWidthText = new();
    private readonly TextBox _summonHeightText = new();

    public SettingsForm(AppSettings settings, Logger logger)
    {
        _settings = settings;
        _logger = logger;

        Text = UiText.SettingsTitle;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        ClientSize = new Size(660, 878);
        BackColor = Color.FromArgb(28, 28, 28);
        ForeColor = Color.FromArgb(235, 235, 235);
        Font = new Font("Segoe UI", 9f);

        BuildControls();
        LoadValues();
    }

    private void BuildControls()
    {
        var title = new Label
        {
            Text = UiText.SettingsTitle,
            Font = new Font(Font.FontFamily, 13f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(18, 16),
            ForeColor = Color.White
        };
        Controls.Add(title);

        AddLabel(UiText.ShowHotkeySetting, 18, 62);
        SetupTextBox(_showHotkeyText, 260, 58, 220);
        Controls.Add(_showHotkeyText);

        AddLabel(UiText.RestoreHotkeySetting, 18, 102);
        SetupTextBox(_restoreHotkeyText, 260, 98, 220);
        Controls.Add(_restoreHotkeyText);

        AddLabel(UiText.TargetMonitorSetting, 18, 146);
        SetupCombo(_targetMonitorCombo, 260, 140, 220);
        Controls.Add(_targetMonitorCombo);

        AddLabel(UiText.PopupPlacementSetting, 18, 190);
        SetupCombo(_popupModeCombo, 260, 184, 220);
        _popupModeCombo.Items.Add(new PopupModeChoice("Cursor", UiText.PopupAtCursor));
        _popupModeCombo.Items.Add(new PopupModeChoice("Primary", UiText.PopupAtPrimary));
        _popupModeCombo.Items.Add(new PopupModeChoice("Target", UiText.PopupAtTarget));
        _popupModeCombo.Items.Add(new PopupModeChoice("SpecificMonitor", UiText.PopupAtSpecific));
        _popupModeCombo.SelectedIndexChanged += (_, _) => _popupMonitorCombo.Enabled = SelectedPopupMode() == "SpecificMonitor";
        Controls.Add(_popupModeCombo);

        AddLabel(UiText.PopupMonitorSetting, 18, 232);
        SetupCombo(_popupMonitorCombo, 260, 226, 220);
        Controls.Add(_popupMonitorCombo);

        _closeAfterSummonCheck.Text = UiText.CloseAfterSummon;
        _closeAfterSummonCheck.AutoSize = true;
        _closeAfterSummonCheck.Location = new Point(18, 270);
        _closeAfterSummonCheck.ForeColor = Color.FromArgb(235, 235, 235);
        Controls.Add(_closeAfterSummonCheck);

        _moveCursorToTrayCheck.Text = UiText.MoveCursorToTray;
        _moveCursorToTrayCheck.AutoSize = true;
        _moveCursorToTrayCheck.Location = new Point(18, 298);
        _moveCursorToTrayCheck.ForeColor = Color.FromArgb(235, 235, 235);
        Controls.Add(_moveCursorToTrayCheck);

        _preferExactTrayIconCheck.Text = UiText.PreferExactTrayIcon;
        _preferExactTrayIconCheck.AutoSize = true;
        _preferExactTrayIconCheck.Location = new Point(18, 326);
        _preferExactTrayIconCheck.ForeColor = Color.FromArgb(235, 235, 235);
        Controls.Add(_preferExactTrayIconCheck);

        _keepPickerFocusedCheck.Text = UiText.KeepPickerFocused;
        _keepPickerFocusedCheck.AutoSize = true;
        _keepPickerFocusedCheck.Location = new Point(18, 354);
        _keepPickerFocusedCheck.ForeColor = Color.FromArgb(235, 235, 235);
        Controls.Add(_keepPickerFocusedCheck);

        _showWindowThumbnailsCheck.Text = UiText.ShowWindowThumbnails;
        _showWindowThumbnailsCheck.AutoSize = true;
        _showWindowThumbnailsCheck.Location = new Point(18, 382);
        _showWindowThumbnailsCheck.ForeColor = Color.FromArgb(235, 235, 235);
        Controls.Add(_showWindowThumbnailsCheck);

        _showWindowListCheck.Text = UiText.ShowWindowList;
        _showWindowListCheck.AutoSize = true;
        _showWindowListCheck.Location = new Point(18, 410);
        _showWindowListCheck.ForeColor = Color.FromArgb(235, 235, 235);
        Controls.Add(_showWindowListCheck);

        _enableAltItemHotkeysCheck.Text = UiText.EnableAltItemHotkeys;
        _enableAltItemHotkeysCheck.AutoSize = true;
        _enableAltItemHotkeysCheck.Location = new Point(18, 438);
        _enableAltItemHotkeysCheck.ForeColor = Color.FromArgb(235, 235, 235);
        Controls.Add(_enableAltItemHotkeysCheck);

        _enableMonitorScreenSaverCheck.Text = UiText.EnableMonitorScreenSaver;
        _enableMonitorScreenSaverCheck.AutoSize = true;
        _enableMonitorScreenSaverCheck.Location = new Point(18, 466);
        _enableMonitorScreenSaverCheck.ForeColor = Color.FromArgb(235, 235, 235);
        Controls.Add(_enableMonitorScreenSaverCheck);

        _suppressMonitorScreenSaverWhenMediaVisibleCheck.Text = UiText.SuppressMonitorScreenSaverWhenMediaVisible;
        _suppressMonitorScreenSaverWhenMediaVisibleCheck.AutoSize = true;
        _suppressMonitorScreenSaverWhenMediaVisibleCheck.Location = new Point(42, 494);
        _suppressMonitorScreenSaverWhenMediaVisibleCheck.ForeColor = Color.FromArgb(235, 235, 235);
        Controls.Add(_suppressMonitorScreenSaverWhenMediaVisibleCheck);

        _showMonitorScreenSaverRemainingTimeCheck.Text = UiText.ShowMonitorScreenSaverRemainingTime;
        _showMonitorScreenSaverRemainingTimeCheck.AutoSize = true;
        _showMonitorScreenSaverRemainingTimeCheck.Location = new Point(42, 522);
        _showMonitorScreenSaverRemainingTimeCheck.ForeColor = Color.FromArgb(235, 235, 235);
        Controls.Add(_showMonitorScreenSaverRemainingTimeCheck);

        AddLabel(UiText.MonitorScreenSaverIdleMinutes, 42, 554);
        SetupTextBox(_monitorScreenSaverIdleMinutesText, 260, 550, 72);
        Controls.Add(_monitorScreenSaverIdleMinutesText);

        AddLabel(UiText.TapoControlUrlSetting, 42, 586);
        SetupTextBox(_tapoControlUrlText, 260, 582, 350);
        Controls.Add(_tapoControlUrlText);

        AddLabel(UiText.MonitorPowerControlDelayMinutesSetting, 42, 618);
        SetupTextBox(_monitorPowerControlDelayMinutesText, 430, 614, 72);
        Controls.Add(_monitorPowerControlDelayMinutesText);

        _useSummonSizeCheck.Text = UiText.UseSummonSize;
        _useSummonSizeCheck.AutoSize = true;
        _useSummonSizeCheck.Location = new Point(18, 650);
        _useSummonSizeCheck.ForeColor = Color.FromArgb(235, 235, 235);
        _useSummonSizeCheck.CheckedChanged += (_, _) => UpdateSummonSizeEnabled();
        Controls.Add(_useSummonSizeCheck);

        AddLabel(UiText.SummonSizeSetting, 42, 684);
        AddLabel(UiText.SummonWidthSetting, 160, 684);
        SetupTextBox(_summonWidthText, 196, 680, 72);
        Controls.Add(_summonWidthText);
        AddLabel("x", 276, 684);
        AddLabel(UiText.SummonHeightSetting, 300, 684);
        SetupTextBox(_summonHeightText, 346, 680, 72);
        Controls.Add(_summonHeightText);

        var hint = new Label
        {
            Text = UiText.HotkeyExample,
            AutoSize = true,
            Location = new Point(18, 728),
            ForeColor = Color.FromArgb(170, 170, 170)
        };
        Controls.Add(hint);

        var saveButton = new Button
        {
            Text = UiText.Save,
            DialogResult = DialogResult.None,
            Location = new Point(438, 812),
            Size = new Size(82, 28),
            BackColor = Color.FromArgb(55, 86, 125),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        saveButton.Click += (_, _) => SaveClicked();
        Controls.Add(saveButton);

        var cancelButton = new Button
        {
            Text = UiText.Cancel,
            DialogResult = DialogResult.Cancel,
            Location = new Point(532, 812),
            Size = new Size(82, 28),
            BackColor = Color.FromArgb(55, 55, 55),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        Controls.Add(cancelButton);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private void AddLabel(string text, int x, int y)
    {
        Controls.Add(new Label
        {
            Text = text,
            AutoSize = true,
            Location = new Point(x, y),
            ForeColor = Color.FromArgb(220, 220, 220)
        });
    }

    private static void SetupTextBox(TextBox box, int x, int y, int width)
    {
        box.Location = new Point(x, y);
        box.Size = new Size(width, 26);
        box.BackColor = Color.FromArgb(38, 38, 38);
        box.ForeColor = Color.White;
        box.BorderStyle = BorderStyle.FixedSingle;
    }

    private static void SetupCombo(ComboBox combo, int x, int y, int width)
    {
        combo.Location = new Point(x, y);
        combo.Size = new Size(width, 26);
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.BackColor = Color.FromArgb(38, 38, 38);
        combo.ForeColor = Color.White;
    }

    private void LoadValues()
    {
        _showHotkeyText.Text = _settings.Hotkey;
        _restoreHotkeyText.Text = _settings.RestoreHotkey;
        _closeAfterSummonCheck.Checked = _settings.ClosePopupAfterSummon;
        _moveCursorToTrayCheck.Checked = _settings.MoveCursorToTrayOnHotkey;
        _preferExactTrayIconCheck.Checked = _settings.PreferExactTrayIconPosition;
        _keepPickerFocusedCheck.Checked = _settings.KeepPickerFocused;
        _showWindowThumbnailsCheck.Checked = _settings.ShowWindowThumbnails;
        _showWindowListCheck.Checked = _settings.ShowWindowList;
        _enableAltItemHotkeysCheck.Checked = _settings.EnableAltItemHotkeys;
        _enableMonitorScreenSaverCheck.Checked = _settings.EnableMonitorScreenSaver;
        _suppressMonitorScreenSaverWhenMediaVisibleCheck.Checked = _settings.SuppressMonitorScreenSaverWhenMediaVisible;
        _showMonitorScreenSaverRemainingTimeCheck.Checked = _settings.ShowMonitorScreenSaverRemainingTime;
        _monitorScreenSaverIdleMinutesText.Text = _settings.MonitorScreenSaverIdleMinutes.ToString();
        _tapoControlUrlText.Text = _settings.TapoControlUrl;
        _monitorPowerControlDelayMinutesText.Text = _settings.MonitorPowerControlDelayMinutes.ToString();
        _useSummonSizeCheck.Checked = _settings.UseSummonSize;
        _summonWidthText.Text = _settings.SummonWidth.ToString();
        _summonHeightText.Text = _settings.SummonHeight.ToString();
        UpdateSummonSizeEnabled();

        _targetMonitorCombo.Items.Clear();
        _targetMonitorCombo.Items.Add(new MonitorChoice(-1, null, UiText.WindowsPrimaryDisplay));
        var screens = Screen.AllScreens;
        for (var i = 0; i < screens.Length; i++)
        {
            var s = screens[i];
            var label = $"{UiText.Monitor(i + 1)}  {s.DeviceName}";
            if (Screen.PrimaryScreen?.DeviceName == s.DeviceName)
                label += $"  [{UiText.PrimaryTag}]";
            _targetMonitorCombo.Items.Add(new MonitorChoice(i, s.DeviceName, label));
        }

        var selectedTarget = 0;
        if (!_settings.UsePrimaryScreen && !string.IsNullOrWhiteSpace(_settings.TargetMonitorDeviceName))
        {
            for (var i = 1; i < _targetMonitorCombo.Items.Count; i++)
            {
                if (_targetMonitorCombo.Items[i] is MonitorChoice c && c.DeviceName == _settings.TargetMonitorDeviceName)
                {
                    selectedTarget = i;
                    break;
                }
            }
        }
        else if (!_settings.UsePrimaryScreen)
        {
            selectedTarget = Math.Clamp(_settings.MainMonitorIndex + 1, 1, Math.Max(1, _targetMonitorCombo.Items.Count - 1));
        }
        _targetMonitorCombo.SelectedIndex = Math.Min(selectedTarget, _targetMonitorCombo.Items.Count - 1);

        _popupModeCombo.SelectedIndex = 0;
        for (var i = 0; i < _popupModeCombo.Items.Count; i++)
        {
            if (_popupModeCombo.Items[i] is PopupModeChoice c && c.Value.Equals(_settings.PopupPlacementMode, StringComparison.OrdinalIgnoreCase))
            {
                _popupModeCombo.SelectedIndex = i;
                break;
            }
        }

        _popupMonitorCombo.Items.Clear();
        for (var i = 0; i < screens.Length; i++)
        {
            var s = screens[i];
            var label = $"{UiText.Monitor(i + 1)}  {s.DeviceName}";
            if (Screen.PrimaryScreen?.DeviceName == s.DeviceName)
                label += $"  [{UiText.PrimaryTag}]";
            _popupMonitorCombo.Items.Add(new MonitorChoice(i, s.DeviceName, label));
        }

        if (_popupMonitorCombo.Items.Count > 0)
        {
            var selectedPopup = 0;
            if (!string.IsNullOrWhiteSpace(_settings.PopupMonitorDeviceName))
            {
                for (var i = 0; i < _popupMonitorCombo.Items.Count; i++)
                {
                    if (_popupMonitorCombo.Items[i] is MonitorChoice c && c.DeviceName == _settings.PopupMonitorDeviceName)
                    {
                        selectedPopup = i;
                        break;
                    }
                }
            }
            _popupMonitorCombo.SelectedIndex = selectedPopup;
        }
        _popupMonitorCombo.Enabled = SelectedPopupMode() == "SpecificMonitor";
    }

    private string SelectedPopupMode()
    {
        return _popupModeCombo.SelectedItem is PopupModeChoice c ? c.Value : "Cursor";
    }

    private void UpdateSummonSizeEnabled()
    {
        var enabled = _useSummonSizeCheck.Checked;
        _summonWidthText.Enabled = enabled;
        _summonHeightText.Enabled = enabled;
    }

    private void SaveClicked()
    {
        if (!HotkeyParser.TryParse(_showHotkeyText.Text, out var showHotkey, out var showError))
        {
            MessageBox.Show(this, showError, "WinPicker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        if (!HotkeyParser.TryParse(_restoreHotkeyText.Text, out var restoreHotkey, out var restoreError))
        {
            MessageBox.Show(this, restoreError, "WinPicker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        if (showHotkey.NormalizedText.Equals(restoreHotkey.NormalizedText, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, UiText.HotkeysMustDiffer, "WinPicker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        if (!int.TryParse(_monitorScreenSaverIdleMinutesText.Text.Trim(), out var monitorSaverIdleMinutes) || monitorSaverIdleMinutes < 1 || monitorSaverIdleMinutes > 240)
        {
            MessageBox.Show(this, "Screen saver idle time must be between 1 and 240 minutes.", "WinPicker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        var tapoControlUrl = _tapoControlUrlText.Text.Trim();
        if (!Uri.TryCreate(tapoControlUrl, UriKind.Absolute, out var tapoUri) ||
            (tapoUri.Scheme != Uri.UriSchemeHttp && tapoUri.Scheme != Uri.UriSchemeHttps))
        {
            MessageBox.Show(this, UiText.InvalidTapoControlUrl, "WinPicker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        if (!int.TryParse(_monitorPowerControlDelayMinutesText.Text.Trim(), out var powerDelayMinutes) || powerDelayMinutes < 0 || powerDelayMinutes > 240)
        {
            MessageBox.Show(this, UiText.InvalidMonitorPowerControlDelay, "WinPicker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        if (!int.TryParse(_summonWidthText.Text.Trim(), out var summonWidth) || summonWidth < 320 || summonWidth > 16000)
        {
            MessageBox.Show(this, "Summon width must be between 320 and 16000.", "WinPicker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        if (!int.TryParse(_summonHeightText.Text.Trim(), out var summonHeight) || summonHeight < 200 || summonHeight > 16000)
        {
            MessageBox.Show(this, "Summon height must be between 200 and 16000.", "WinPicker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        _settings.Hotkey = showHotkey.NormalizedText;
        _settings.RestoreHotkey = restoreHotkey.NormalizedText;
        _settings.ClosePopupAfterSummon = _closeAfterSummonCheck.Checked;
        _settings.MoveCursorToTrayOnHotkey = _moveCursorToTrayCheck.Checked;
        _settings.PreferExactTrayIconPosition = _preferExactTrayIconCheck.Checked;
        _settings.KeepPickerFocused = _keepPickerFocusedCheck.Checked;
        _settings.ShowWindowThumbnails = _showWindowThumbnailsCheck.Checked;
        _settings.ShowWindowList = _showWindowListCheck.Checked;
        _settings.EnableAltItemHotkeys = _enableAltItemHotkeysCheck.Checked;
        _settings.EnableMonitorScreenSaver = _enableMonitorScreenSaverCheck.Checked;
        _settings.SuppressMonitorScreenSaverWhenMediaVisible = _suppressMonitorScreenSaverWhenMediaVisibleCheck.Checked;
        _settings.ShowMonitorScreenSaverRemainingTime = _showMonitorScreenSaverRemainingTimeCheck.Checked;
        _settings.MonitorScreenSaverIdleMinutes = monitorSaverIdleMinutes;
        _settings.TapoControlUrl = tapoControlUrl.TrimEnd('?', '&');
        _settings.MonitorPowerControlDelayMinutes = powerDelayMinutes;
        _settings.UseSummonSize = _useSummonSizeCheck.Checked;
        _settings.SummonWidth = summonWidth;
        _settings.SummonHeight = summonHeight;

        if (_targetMonitorCombo.SelectedItem is MonitorChoice target)
        {
            if (target.Index < 0)
            {
                _settings.UsePrimaryScreen = true;
                _settings.TargetMonitorDeviceName = null;
                _settings.MainMonitorIndex = 0;
            }
            else
            {
                _settings.UsePrimaryScreen = false;
                _settings.MainMonitorIndex = target.Index;
                _settings.TargetMonitorDeviceName = target.DeviceName;
            }
        }

        _settings.PopupPlacementMode = SelectedPopupMode();
        if (_popupMonitorCombo.SelectedItem is MonitorChoice popup)
            _settings.PopupMonitorDeviceName = popup.DeviceName;

        SettingsService.Save(_settings, _logger);
        SettingsService.SaveTargetMonitorToRegistry(_settings, _logger);
        _logger.Info("Settings saved from SettingsForm.");
        DialogResult = DialogResult.OK;
        Close();
    }

    private sealed record MonitorChoice(int Index, string? DeviceName, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record PopupModeChoice(string Value, string Label)
    {
        public override string ToString() => Label;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        TopMost = true;
        Activate();
        BringToFront();
    }

}
