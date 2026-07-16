namespace WinPicker;

public sealed class SplashForm : Form
{
    private readonly Image? _logo;

    public SplashForm()
    {
        _logo = BrandingImageLoader.LoadImage("WinPickerSplash.png")
            ?? BrandingImageLoader.LoadImage("CyfomixAbout.png");

        Text = UiText.AppTitleWithVersion;
        Icon = IconLoader.LoadAppIcon();
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        ClientSize = new Size(560, 260);
        BackColor = Color.FromArgb(24, 18, 38);
        ForeColor = Color.FromArgb(245, 245, 250);
        Font = new Font("Segoe UI", 10f);
        DoubleBuffered = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(20, 16, 30));

        using var bg = new System.Drawing.Drawing2D.LinearGradientBrush(
            ClientRectangle,
            Color.FromArgb(84, 38, 150),
            Color.FromArgb(24, 18, 38),
            35f);
        g.FillRectangle(bg, ClientRectangle);

        using var borderPen = new Pen(Color.FromArgb(170, 135, 255), 1.4f);
        g.DrawRectangle(borderPen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);

        var imageRect = new Rectangle(28, 34, 192, 192);
        using (var imageBack = new SolidBrush(Color.FromArgb(42, 30, 70)))
            g.FillRectangle(imageBack, imageRect);

        if (_logo is not null)
            g.DrawImage(_logo, imageRect);

        using var titleFont = new Font("Segoe UI", 24f, FontStyle.Bold);
        using var versionFont = new Font("Segoe UI", 13f, FontStyle.Bold);
        using var textFont = new Font("Segoe UI", 10f, FontStyle.Regular);
        using var titleBrush = new SolidBrush(Color.White);
        using var versionBrush = new SolidBrush(Color.FromArgb(255, 226, 120));
        using var textBrush = new SolidBrush(Color.FromArgb(225, 220, 235));

        var textX = 250;
        g.DrawString(UiText.AppName, titleFont, titleBrush, textX, 58);
        g.DrawString($"Ver {VersionInfoService.Current.NormalizedVersion}", versionFont, versionBrush, textX + 4, 104);
        g.DrawString(UiText.SplashStarting, textFont, textBrush, textX + 4, 145);
        g.DrawString(UiText.SplashDescription, textFont, textBrush, new RectangleF(textX + 4, 174, 280, 50));

        using var glowPen = new Pen(Color.FromArgb(120, 255, 210, 90), 3f);
        g.DrawLine(glowPen, textX + 4, 132, ClientSize.Width - 34, 132);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _logo?.Dispose();

        base.Dispose(disposing);
    }
}
