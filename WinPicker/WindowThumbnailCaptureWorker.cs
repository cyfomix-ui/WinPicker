using System.Drawing.Drawing2D;

namespace WinPicker;

internal static class WindowThumbnailCaptureWorker
{
    private const int MaxSourceWidth = 6000;
    private const int MaxSourceHeight = 4000;

    public static Bitmap? Capture(WindowSnapshot target, CancellationToken cancellationToken)
    {
        if (target.Handle == IntPtr.Zero || target.IsMinimized || !NativeMethods.IsWindow(target.Handle))
            return null;

        cancellationToken.ThrowIfCancellationRequested();
        if (!NativeMethods.GetWindowRect(target.Handle, out var nativeRect))
            return null;

        var bounds = nativeRect.ToRectangle();
        if (bounds.Width <= 0 || bounds.Height <= 0 || bounds.Width > MaxSourceWidth || bounds.Height > MaxSourceHeight)
            return null;

        NativeMethods.GetWindowThreadProcessId(target.Handle, out var pid);
        if (target.ProcessId != 0 && pid != target.ProcessId)
            return null;

        using var full = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(full))
        {
            graphics.Clear(Color.FromArgb(24, 24, 24));
            var hdc = graphics.GetHdc();
            try
            {
                // Do not use CopyFromScreen here: an obscured window would produce a misleading image.
                if (!NativeMethods.PrintWindow(target.Handle, hdc, NativeMethods.PW_RENDERFULLCONTENT))
                    return null;
            }
            finally
            {
                graphics.ReleaseHdc(hdc);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var fit = FitSize(bounds.Size, target.MaximumSize);
        var thumbnail = new Bitmap(fit.Width, fit.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(thumbnail))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            graphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;
            graphics.CompositingQuality = CompositingQuality.HighSpeed;
            graphics.DrawImage(full, new Rectangle(Point.Empty, thumbnail.Size));
        }

        return thumbnail;
    }

    private static Size FitSize(Size source, Size limit)
    {
        var width = Math.Max(1, limit.Width);
        var height = Math.Max(1, limit.Height);
        var scale = Math.Min(width / (float)source.Width, height / (float)source.Height);
        scale = Math.Clamp(scale, 0.03f, 1f);
        return new Size(
            Math.Max(1, (int)Math.Round(source.Width * scale)),
            Math.Max(1, (int)Math.Round(source.Height * scale)));
    }
}

internal sealed record WindowSnapshot(
    IntPtr Handle,
    uint ProcessId,
    Rectangle Bounds,
    bool IsMinimized,
    Size MaximumSize,
    int Priority);
