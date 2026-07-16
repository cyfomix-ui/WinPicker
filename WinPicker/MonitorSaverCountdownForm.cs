namespace WinPicker;

public sealed class MonitorSaverCountdownForm : Form
{
    private readonly Label _label = new();
    private readonly Screen _screen;

    public MonitorSaverCountdownForm(Screen screen)
    {
        _screen = screen;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        AutoScaleMode = AutoScaleMode.Dpi;
        Padding = Padding.Empty;

        _label.AutoSize = true;
        _label.BackColor = Color.Transparent;
        _label.ForeColor = Color.White;
        _label.Font = CreateDisplayFont();
        _label.TextAlign = ContentAlignment.MiddleRight;
        _label.Padding = new Padding(2);
        Controls.Add(_label);

        UpdateDisplay(TimeSpan.FromMinutes(10));
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE | 0x00000020; // WS_EX_TRANSPARENT
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    public void UpdateDisplay(TimeSpan remaining)
    {
        if (IsDisposed)
            return;

        var seconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
        var minutesPart = seconds / 60;
        var secondsPart = seconds % 60;
        _label.Text = $"{DateTime.Now:HH:mm}  {minutesPart}:{secondsPart:00}";
        _label.PerformLayout();
        ClientSize = _label.PreferredSize;

        var area = _screen.WorkingArea;
        Location = new Point(
            Math.Max(area.Left, area.Right - Width - 16),
            area.Top + 12);
    }

    private static Font CreateDisplayFont()
    {
        try
        {
            return new Font("Impact", 14f, FontStyle.Regular, GraphicsUnit.Point);
        }
        catch
        {
            return new Font("Arial", 14f, FontStyle.Bold, GraphicsUnit.Point);
        }
    }
}
