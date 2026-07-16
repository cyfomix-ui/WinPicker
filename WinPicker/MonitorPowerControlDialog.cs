namespace WinPicker;

public sealed class MonitorPowerControlDialog : Form
{
    private readonly TextBox _ipText = new();
    public string DeviceIp => _ipText.Text.Trim();

    public MonitorPowerControlDialog(string currentIp)
    {
        Text = UiText.MonitorPowerControlDialogTitle;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        ClientSize = new Size(440, 155);
        BackColor = Color.FromArgb(28, 28, 28);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9f);

        Controls.Add(new Label
        {
            Text = UiText.MonitorPowerControlDialogMessage,
            AutoSize = true,
            Location = new Point(18, 18),
            ForeColor = Color.FromArgb(225, 225, 225)
        });

        _ipText.Location = new Point(18, 52);
        _ipText.Size = new Size(400, 26);
        _ipText.Text = currentIp ?? string.Empty;
        _ipText.BackColor = Color.FromArgb(38, 38, 38);
        _ipText.ForeColor = Color.White;
        _ipText.BorderStyle = BorderStyle.FixedSingle;
        Controls.Add(_ipText);

        var ok = new Button
        {
            Text = UiText.Save,
            Location = new Point(242, 104),
            Size = new Size(82, 28),
            DialogResult = DialogResult.OK,
            BackColor = Color.FromArgb(55, 86, 125),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        ok.Click += (_, _) =>
        {
            var value = DeviceIp;
            if (value.Length > 0 && !System.Net.IPAddress.TryParse(value, out _))
            {
                MessageBox.Show(this, IsJapaneseText(), "WinPicker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        };
        Controls.Add(ok);

        var cancel = new Button
        {
            Text = UiText.Cancel,
            Location = new Point(336, 104),
            Size = new Size(82, 28),
            DialogResult = DialogResult.Cancel,
            BackColor = Color.FromArgb(55, 55, 55),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;

        Shown += (_, _) =>
        {
            Activate();
            _ipText.Focus();
            _ipText.SelectAll();
        };
    }

    private static string IsJapaneseText() => UiText.IsJapanese
        ? "IPv4またはIPv6アドレスを入力してください。"
        : "Enter a valid IPv4 or IPv6 address.";
}
