namespace WinPicker;

public sealed class Logger : IDisposable
{
    private readonly object _sync = new();
    private readonly string _logDirectory;

    public Logger()
    {
        _logDirectory = AppPaths.LogsDirectory;
    }

    public void Info(string message) => Write("INFO", message);

    public void Warn(string message) => Write("WARN", message);

    public void Error(string message, Exception? ex = null)
    {
        if (ex is null)
        {
            Write("ERROR", message);
            return;
        }

        Write("ERROR", $"{message}{Environment.NewLine}{ex}");
    }

    public void Dispose()
    {
        // No unmanaged resources. Kept so Program.cs can use deterministic cleanup semantics.
    }

    private void Write(string level, string message)
    {
        try
        {
            var path = Path.Combine(_logDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}{Environment.NewLine}";
            lock (_sync)
            {
                File.AppendAllText(path, line);
            }
        }
        catch
        {
            // Logging must never crash the tray app.
        }
    }
}
