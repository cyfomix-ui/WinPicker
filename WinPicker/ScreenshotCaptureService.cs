namespace WinPicker;

public static class ScreenshotCaptureService
{
    public static string CaptureAllScreens(Logger logger)
    {
        var screens = Screen.AllScreens;
        if (screens.Length == 0)
            throw new InvalidOperationException("No screens found.");

        var left = screens.Min(s => s.Bounds.Left);
        var top = screens.Min(s => s.Bounds.Top);
        var right = screens.Max(s => s.Bounds.Right);
        var bottom = screens.Max(s => s.Bounds.Bottom);
        var bounds = Rectangle.FromLTRB(left, top, right, bottom);

        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        var folder = Path.Combine(pictures, "WinPicker");
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, $"{DateTime.Now:yyyyMMdd_HHmmss}.jpg");

        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
        }

        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Jpeg);
        logger.Info($"Screenshot saved: {path}");
        return path;
    }
}
