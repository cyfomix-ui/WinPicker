namespace WinPicker;

internal static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var logger = CreateLogger();
        _mutex = new Mutex(initiallyOwned: true, name: "WinPicker.SingleInstance", out var createdNew);

        if (!createdNew)
        {
            MessageBox.Show(
                "WinPicker is already running.",
                "WinPicker",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        SplashForm? splash = null;

        try
        {
            var splashStartedAt = DateTime.UtcNow;
            splash = new SplashForm();
            splash.Show();
            Application.DoEvents();

            var settings = SettingsService.Load(logger);
            var context = new TrayApplicationContext(settings, logger);

            var elapsed = (int)(DateTime.UtcNow - splashStartedAt).TotalMilliseconds;
            var remaining = 850 - elapsed;
            if (remaining > 0)
            {
                Thread.Sleep(remaining);
                Application.DoEvents();
            }

            if (!splash.IsDisposed)
                splash.Close();
            splash.Dispose();
            splash = null;

            Application.Run(context);
        }
        catch (Exception ex)
        {
            logger.Error("Fatal error.", ex);
            MessageBox.Show(
                ex.Message,
                "WinPicker fatal error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            try
            {
                if (splash is not null && !splash.IsDisposed)
                    splash.Dispose();
            }
            catch
            {
                // Ignore.
            }

            try
            {
                _mutex?.ReleaseMutex();
            }
            catch
            {
                // Ignore.
            }

            _mutex?.Dispose();
        }
    }

    private static Logger CreateLogger()
    {
        return new Logger();
    }
}
