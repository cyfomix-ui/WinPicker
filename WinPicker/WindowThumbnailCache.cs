namespace WinPicker;

public sealed class WindowThumbnailCache : IDisposable
{
    private const int MaxThumbnailWidth = 420;
    private const int MaxThumbnailHeight = 260;

    private readonly Dictionary<IntPtr, Bitmap> _cache = new();
    private readonly Logger _logger;

    public WindowThumbnailCache(Logger logger)
    {
        _logger = logger;
    }

    public Bitmap? GetThumbnail(WindowInfo window)
    {
        if (window.Handle == IntPtr.Zero || window.IsMinimized)
            return null;

        if (_cache.TryGetValue(window.Handle, out var cached))
            return cached;

        var captured = CaptureThumbnail(window);
        if (captured is not null)
            _cache[window.Handle] = captured;

        return captured;
    }

    public void Clear()
    {
        foreach (var bitmap in _cache.Values)
            bitmap.Dispose();

        _cache.Clear();
    }

    private Bitmap? CaptureThumbnail(WindowInfo window)
    {
        var bounds = window.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return null;

        // Avoid very large temporary bitmaps. The fallback still draws title rectangles.
        if (bounds.Width > 6000 || bounds.Height > 4000)
            return null;

        Bitmap? full = null;
        try
        {
            full = new Bitmap(Math.Max(1, bounds.Width), Math.Max(1, bounds.Height));
            using (var g = Graphics.FromImage(full))
            {
                g.Clear(Color.FromArgb(24, 24, 24));
                var hdc = g.GetHdc();
                try
                {
                    var ok = NativeMethods.PrintWindow(window.Handle, hdc, NativeMethods.PW_RENDERFULLCONTENT);
                    if (!ok)
                    {
                        g.ReleaseHdc(hdc);
                        hdc = IntPtr.Zero;
                        g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
                    }
                }
                finally
                {
                    if (hdc != IntPtr.Zero)
                        g.ReleaseHdc(hdc);
                }
            }

            var fit = FitSize(bounds.Size, new Size(MaxThumbnailWidth, MaxThumbnailHeight));
            var thumb = new Bitmap(Math.Max(1, fit.Width), Math.Max(1, fit.Height));
            using (var tg = Graphics.FromImage(thumb))
            {
                tg.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                tg.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                tg.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                tg.Clear(Color.FromArgb(24, 24, 24));
                tg.DrawImage(full, new Rectangle(Point.Empty, thumb.Size));
            }

            return thumb;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to capture thumbnail for hwnd=0x{window.Handle.ToInt64():X} title=\"{window.Title}\"", ex);
            return null;
        }
        finally
        {
            full?.Dispose();
        }
    }

    private static Size FitSize(Size source, Size limit)
    {
        if (source.Width <= 0 || source.Height <= 0)
            return new Size(1, 1);

        var scale = Math.Min(limit.Width / (float)source.Width, limit.Height / (float)source.Height);
        scale = Math.Min(1.0f, Math.Max(0.05f, scale));
        return new Size(
            Math.Max(1, (int)Math.Round(source.Width * scale)),
            Math.Max(1, (int)Math.Round(source.Height * scale)));
    }

    public void Dispose()
    {
        Clear();
    }
}
