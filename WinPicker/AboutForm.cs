namespace WinPicker;

public sealed class AboutForm : Form
{
    private readonly Image? _aboutLogo;

    public AboutForm(AppSettings settings)
    {
        _aboutLogo = BrandingImageLoader.LoadImage("CyfomixAbout.png");

        Text = UiText.About;
        Icon = IconLoader.LoadAppIcon();
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(720, 450);
        Font = new Font("Segoe UI", 10f);
        BackColor = Color.FromArgb(24, 24, 24);
        ForeColor = Color.FromArgb(235, 235, 235);

        var logo = new PictureBox
        {
            Left = 20,
            Top = 22,
            Width = 192,
            Height = 192,
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = _aboutLogo,
            BackColor = Color.Black,
            BorderStyle = BorderStyle.FixedSingle
        };
        Controls.Add(logo);

        var textBox = new TextBox
        {
            Left = 232,
            Top = 24,
            Width = 460,
            Height = 315,
            Multiline = true,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(24, 24, 24),
            ForeColor = Color.FromArgb(235, 235, 235),
            Text = UiText.AboutText(settings).Replace("\n", Environment.NewLine),
            TabStop = false,
            ScrollBars = ScrollBars.None,
            WordWrap = true
        };
        Controls.Add(textBox);

        var github = new LinkLabel
        {
            Left = 232,
            Top = 350,
            Width = 460,
            Height = 26,
            Text = UiText.GitHubUrl,
            BackColor = Color.FromArgb(24, 24, 24),
            LinkColor = Color.FromArgb(130, 180, 255),
            ActiveLinkColor = Color.FromArgb(180, 210, 255),
            VisitedLinkColor = Color.FromArgb(190, 150, 255)
        };
        github.LinkClicked += (_, _) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = UiText.GitHubUrl,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Ignore browser launch failures.
            }
        };
        Controls.Add(github);

        var ok = new Button
        {
            Text = "OK",
            Width = 110,
            Height = 34,
            Left = ClientSize.Width - 130,
            Top = ClientSize.Height - 54,
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
            BackColor = Color.FromArgb(45, 45, 45),
            ForeColor = Color.FromArgb(245, 245, 245),
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.OK
        };
        ok.FlatAppearance.BorderColor = Color.FromArgb(95, 95, 95);
        Controls.Add(ok);

        AcceptButton = ok;
        CancelButton = ok;
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
            // Older Windows builds may ignore this. The form body remains dark.
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _aboutLogo?.Dispose();
        }

        base.Dispose(disposing);
    }
}
