namespace WinPicker;

public sealed class GeometryNameDialog : Form
{
    private readonly TextBox _textBox = new();

    public string SnapshotName => _textBox.Text.Trim();

    public GeometryNameDialog()
    {
        Text = UiText.GeometrySaveTitle;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(420, 150);
        BackColor = Color.FromArgb(28, 28, 28);
        ForeColor = Color.FromArgb(235, 235, 235);
        Font = new Font("Segoe UI", 9f);

        Controls.Add(new Label
        {
            Text = UiText.GeometryNamePrompt,
            Left = 16,
            Top = 16,
            Width = 390,
            Height = 36,
            ForeColor = Color.FromArgb(235, 235, 235)
        });

        _textBox.Left = 16;
        _textBox.Top = 58;
        _textBox.Width = 386;
        _textBox.BackColor = Color.FromArgb(38, 38, 38);
        _textBox.ForeColor = Color.White;
        _textBox.BorderStyle = BorderStyle.FixedSingle;
        Controls.Add(_textBox);

        var ok = new Button
        {
            Text = "OK",
            Left = 226,
            Top = 104,
            Width = 82,
            Height = 28,
            BackColor = Color.FromArgb(55, 86, 125),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.OK
        };
        ok.Click += (_, _) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };
        Controls.Add(ok);

        var cancel = new Button
        {
            Text = UiText.Cancel,
            Left = 320,
            Top = 104,
            Width = 82,
            Height = 28,
            BackColor = Color.FromArgb(55, 55, 55),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.Cancel
        };
        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        Controls.Add(cancel);

        AcceptButton = ok;
        CancelButton = cancel;
    }
}
