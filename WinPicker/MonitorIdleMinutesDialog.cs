namespace WinPicker;

public sealed class MonitorIdleMinutesDialog : Form
{
    private readonly NumericUpDown _minutes = new();

    public int IdleMinutes => (int)_minutes.Value;

    public MonitorIdleMinutesDialog(int currentMinutes, int globalMinutes)
    {
        Text = UiText.MonitorSaverIdleDialogTitle;
        Icon = IconLoader.LoadAppIcon();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(420, 170);
        BackColor = Color.FromArgb(28, 28, 28);
        ForeColor = Color.FromArgb(235, 235, 235);
        Font = new Font("Segoe UI", 9f);

        var label = new Label
        {
            Text = UiText.MonitorSaverIdleDialogMessage,
            AutoSize = false,
            Location = new Point(18, 18),
            Size = new Size(380, 42),
            ForeColor = Color.FromArgb(235, 235, 235)
        };
        Controls.Add(label);

        var valueLabel = new Label
        {
            Text = UiText.MonitorScreenSaverIdleMinutes,
            AutoSize = true,
            Location = new Point(18, 76),
            ForeColor = Color.FromArgb(220, 220, 220)
        };
        Controls.Add(valueLabel);

        _minutes.Location = new Point(210, 72);
        _minutes.Size = new Size(90, 24);
        _minutes.Minimum = 0;
        _minutes.Maximum = 240;
        _minutes.Value = Math.Clamp(currentMinutes, 0, 240);
        _minutes.BackColor = Color.FromArgb(38, 38, 38);
        _minutes.ForeColor = Color.White;
        Controls.Add(_minutes);

        var globalLabel = new Label
        {
            Text = UiText.MonitorSaverIdleGlobal(globalMinutes),
            AutoSize = true,
            Location = new Point(18, 108),
            ForeColor = Color.FromArgb(170, 170, 170)
        };
        Controls.Add(globalLabel);

        var okButton = new Button
        {
            Text = UiText.Save,
            DialogResult = DialogResult.OK,
            Location = new Point(226, 126),
            Size = new Size(82, 28),
            BackColor = Color.FromArgb(55, 86, 125),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        Controls.Add(okButton);

        var cancelButton = new Button
        {
            Text = UiText.Cancel,
            DialogResult = DialogResult.Cancel,
            Location = new Point(318, 126),
            Size = new Size(82, 28),
            BackColor = Color.FromArgb(55, 55, 55),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        Controls.Add(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }
}
