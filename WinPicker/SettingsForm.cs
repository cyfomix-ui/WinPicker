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
        ClientSize = new Size(560, 548);
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

        var hint = new Label
        {
            Text = UiText.HotkeyExample,
            AutoSize = true,
            Location = new Point(18, 446),
            ForeColor = Color.FromArgb(170, 170, 170)
        };
        Controls.Add(hint);

        var saveButton = new Button
        {
            Text = UiText.Save,
            DialogResult = DialogResult.None,
            Location = new Point(344, 494),
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
            Location = new Point(438, 494),
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

        _settings.Hotkey = showHotkey.NormalizedText;
        _settings.RestoreHotkey = restoreHotkey.NormalizedText;
        _settings.ClosePopupAfterSummon = _closeAfterSummonCheck.Checked;
        _settings.MoveCursorToTrayOnHotkey = _moveCursorToTrayCheck.Checked;
        _settings.PreferExactTrayIconPosition = _preferExactTrayIconCheck.Checked;
        _settings.KeepPickerFocused = _keepPickerFocusedCheck.Checked;
        _settings.ShowWindowThumbnails = _showWindowThumbnailsCheck.Checked;
        _settings.ShowWindowList = _showWindowListCheck.Checked;

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
}
