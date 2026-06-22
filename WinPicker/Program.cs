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

        try
        {
            var settings = SettingsService.Load(logger);
            Application.Run(new TrayApplicationContext(settings, logger));
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
