using System.Drawing.Drawing2D;

namespace WinPicker;

public sealed class ScreenSaverOverlayForm : Form
{
    private const int RandomTextMoveSeconds = 30;
    private const int RandomTextFadeSeconds = 5;

    private readonly string _kind;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Random _random = new();
    private readonly List<LineParticle> _lines = new();
    private readonly List<BubbleParticle> _bubbles = new();

    private string _randomTextLine1 = string.Empty;
    private string _randomTextLine2 = string.Empty;
    private PointF _randomTextLocation = PointF.Empty;
    private Color _randomTextColor = Color.White;
    private float _randomTextFontSize = 30f;
    private SizeF _randomTextBlockSize = SizeF.Empty;
    private DateTime _randomTextCreatedAtUtc = DateTime.UtcNow;

    private int _tick;

    public ScreenSaverOverlayForm(Screen screen, string kind)
    {
        _kind = NormalizeKind(kind);
        Bounds = screen.Bounds;
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Black;
        ForeColor = Color.FromArgb(230, 230, 230);
        DoubleBuffered = true;
        Cursor = Cursors.Default;

        _timer = new System.Windows.Forms.Timer();
        _timer.Interval = _kind switch
        {
            "Lines" => 180,
            "Bubbles" => 90,
            "RandomText" => 500,
            _ => 1000
        };
        _timer.Tick += (_, _) =>
        {
            Advance();
            Invalidate();
        };

        if (_kind == "Lines")
            InitializeLines();
        else if (_kind == "Bubbles")
            InitializeBubbles();
        else if (_kind == "RandomText")
            GenerateRandomTextState();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _timer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Stop();
        _timer.Dispose();
        base.OnFormClosed(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.Clear(Color.Black);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        switch (_kind)
        {
            case "RandomText":
                DrawRandomText(g);
                break;
            case "Lines":
                DrawLines(g);
                break;
            case "Bubbles":
                DrawBubbles(g);
                break;
            default:
                // Black mode intentionally draws nothing else.
                break;
        }
    }

    private void Advance()
    {
        _tick++;

        if (_kind == "RandomText")
        {
            var age = DateTime.UtcNow - _randomTextCreatedAtUtc;
            if (age.TotalSeconds >= RandomTextMoveSeconds)
                GenerateRandomTextState();

            return;
        }

        if (_kind == "Lines")
        {
            foreach (var line in _lines)
            {
                line.X += line.Dx;
                line.Y += line.Dy;
                if (line.X < -80 || line.X > Width + 80 || line.Y < -80 || line.Y > Height + 80)
                {
                    line.X = _random.Next(0, Math.Max(1, Width));
                    line.Y = _random.Next(0, Math.Max(1, Height));
                    line.Dx = _random.Next(-6, 7);
                    line.Dy = _random.Next(-6, 7);
                    if (line.Dx == 0 && line.Dy == 0)
                        line.Dx = 3;
                }
            }

            return;
        }

        if (_kind == "Bubbles")
        {
            foreach (var bubble in _bubbles)
            {
                bubble.X += bubble.Dx;
                bubble.Y += bubble.Dy;

                if (bubble.X < -bubble.Radius * 2)
                    bubble.X = Width + bubble.Radius;
                else if (bubble.X > Width + bubble.Radius * 2)
                    bubble.X = -bubble.Radius;

                if (bubble.Y < -bubble.Radius * 2)
                    bubble.Y = Height + bubble.Radius;
                else if (bubble.Y > Height + bubble.Radius * 2)
                    bubble.Y = -bubble.Radius;
            }
        }
    }

    private void InitializeLines()
    {
        _lines.Clear();
        var count = Math.Max(8, Math.Min(26, Width / 90));
        for (var i = 0; i < count; i++)
        {
            _lines.Add(new LineParticle
            {
                X = _random.Next(0, Math.Max(1, Width)),
                Y = _random.Next(0, Math.Max(1, Height)),
                Dx = _random.Next(-5, 6),
                Dy = _random.Next(-5, 6),
                Length = _random.Next(80, 220)
            });
        }
    }

    private void InitializeBubbles()
    {
        _bubbles.Clear();
        var count = Math.Max(12, Math.Min(42, Width / 55));
        for (var i = 0; i < count; i++)
        {
            var radius = _random.Next(18, 78);
            var dx = (float)(_random.NextDouble() * 1.8 - 0.9);
            var dy = (float)(_random.NextDouble() * 1.8 - 0.9);
            if (Math.Abs(dx) < 0.15f)
                dx = dx < 0 ? -0.25f : 0.25f;
            if (Math.Abs(dy) < 0.15f)
                dy = dy < 0 ? -0.25f : 0.25f;

            _bubbles.Add(new BubbleParticle
            {
                X = _random.Next(0, Math.Max(1, Width)),
                Y = _random.Next(0, Math.Max(1, Height)),
                Radius = radius,
                Dx = dx,
                Dy = dy,
                Color = CreateReadableRandomColor(alpha: _random.Next(45, 125))
            });
        }
    }

    private void DrawRandomText(Graphics g)
    {
        var age = DateTime.UtcNow - _randomTextCreatedAtUtc;
        var alphaFactor = 1.0f;

        if (age.TotalSeconds > RandomTextMoveSeconds - RandomTextFadeSeconds)
        {
            var fadeAge = age.TotalSeconds - (RandomTextMoveSeconds - RandomTextFadeSeconds);
            alphaFactor = Math.Max(0.0f, 1.0f - (float)(fadeAge / RandomTextFadeSeconds));
        }

        var alpha = Math.Clamp((int)Math.Round(_randomTextColor.A * alphaFactor), 0, 255);
        using var textFont = new Font("Segoe UI", _randomTextFontSize, FontStyle.Bold);
        using var smallFont = new Font("Segoe UI", Math.Max(16f, _randomTextFontSize * 0.78f), FontStyle.Bold);
        using var brush = new SolidBrush(Color.FromArgb(alpha, _randomTextColor.R, _randomTextColor.G, _randomTextColor.B));
        using var shadowBrush = new SolidBrush(Color.FromArgb(Math.Clamp(alpha / 2, 0, 150), 0, 0, 0));
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Near
        };

        var rect1 = new RectangleF(_randomTextLocation.X, _randomTextLocation.Y, _randomTextBlockSize.Width, _randomTextBlockSize.Height * 0.55f);
        var rect2 = new RectangleF(_randomTextLocation.X, _randomTextLocation.Y + _randomTextBlockSize.Height * 0.54f, _randomTextBlockSize.Width, _randomTextBlockSize.Height * 0.46f);

        // Shadow.
        var shadowRect1 = rect1;
        var shadowRect2 = rect2;
        shadowRect1.Offset(3, 3);
        shadowRect2.Offset(3, 3);
        g.DrawString(_randomTextLine1, textFont, shadowBrush, shadowRect1, format);
        g.DrawString(_randomTextLine2, smallFont, shadowBrush, shadowRect2, format);

        // Main centered text. Second line is also center-aligned.
        g.DrawString(_randomTextLine1, textFont, brush, rect1, format);
        g.DrawString(_randomTextLine2, smallFont, brush, rect2, format);
    }

    private void GenerateRandomTextState()
    {
        var now = DateTime.Now;

        // Two display patterns:
        // 1) WinPicker + Ver / time
        // 2) date / time
        if (_random.Next(0, 2) == 0)
        {
            _randomTextLine1 = VersionInfoService.Current.TrayTooltip;
            _randomTextLine2 = now.ToString("HH:mm:ss");
        }
        else
        {
            _randomTextLine1 = now.ToString("yyyy/MM/dd");
            _randomTextLine2 = now.ToString("HH:mm:ss");
        }

        _randomTextFontSize = _random.Next(24, 43);
        _randomTextColor = CreateReadableRandomColor(alpha: 235);

        using var bmp = new Bitmap(1, 1);
        using var g = Graphics.FromImage(bmp);
        using var font1 = new Font("Segoe UI", _randomTextFontSize, FontStyle.Bold);
        using var font2 = new Font("Segoe UI", Math.Max(16f, _randomTextFontSize * 0.78f), FontStyle.Bold);
        var size1 = g.MeasureString(_randomTextLine1, font1);
        var size2 = g.MeasureString(_randomTextLine2, font2);

        _randomTextBlockSize = new SizeF(
            Math.Max(size1.Width, size2.Width) + 36,
            size1.Height + size2.Height + 18);

        var maxX = Math.Max(0, Width - (int)Math.Ceiling(_randomTextBlockSize.Width) - 24);
        var maxY = Math.Max(0, Height - (int)Math.Ceiling(_randomTextBlockSize.Height) - 24);

        _randomTextLocation = new PointF(
            _random.Next(12, Math.Max(13, maxX + 1)),
            _random.Next(12, Math.Max(13, maxY + 1)));

        _randomTextCreatedAtUtc = DateTime.UtcNow;
    }

    private Color CreateReadableRandomColor(int alpha)
    {
        // Bright colors only; black background is fixed.
        return Color.FromArgb(
            alpha,
            _random.Next(90, 256),
            _random.Next(90, 256),
            _random.Next(90, 256));
    }

    private void DrawLines(Graphics g)
    {
        using var pen1 = new Pen(Color.FromArgb(80, 120, 180, 255), 2f);
        using var pen2 = new Pen(Color.FromArgb(130, 255, 215, 90), 1.4f);

        foreach (var line in _lines)
        {
            g.DrawLine(pen1, line.X, line.Y, line.X + line.Length, line.Y + line.Length / 3);
            g.DrawLine(pen2, line.X + line.Length / 3, line.Y + line.Length / 2, line.X - line.Length / 2, line.Y + line.Length);
        }
    }

    private void DrawBubbles(Graphics g)
    {
        foreach (var bubble in _bubbles)
        {
            var rect = new RectangleF(
                bubble.X - bubble.Radius,
                bubble.Y - bubble.Radius,
                bubble.Radius * 2,
                bubble.Radius * 2);

            using var path = new GraphicsPath();
            path.AddEllipse(rect);

            using var fill = new SolidBrush(Color.FromArgb(Math.Max(18, bubble.Color.A / 3), bubble.Color.R, bubble.Color.G, bubble.Color.B));
            using var pen = new Pen(bubble.Color, Math.Max(1.2f, bubble.Radius / 18f));
            using var highlight = new SolidBrush(Color.FromArgb(Math.Min(170, bubble.Color.A + 40), 255, 255, 255));

            g.FillPath(fill, path);
            g.DrawPath(pen, path);

            var h = Math.Max(4f, bubble.Radius * 0.26f);
            g.FillEllipse(
                highlight,
                bubble.X - bubble.Radius * 0.38f,
                bubble.Y - bubble.Radius * 0.42f,
                h,
                h);
        }
    }

    public static string NormalizeKind(string? kind)
    {
        return kind switch
        {
            "Off" => "Off",
            "RandomText" => "RandomText",
            "Lines" => "Lines",
            "Bubbles" => "Bubbles",
            "Black" => "Black",
            _ => "Black"
        };
    }

    private sealed class LineParticle
    {
        public int X;
        public int Y;
        public int Dx;
        public int Dy;
        public int Length;
    }

    private sealed class BubbleParticle
    {
        public float X;
        public float Y;
        public float Radius;
        public float Dx;
        public float Dy;
        public Color Color;
    }
}
