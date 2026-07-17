using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace WinPicker;

public enum AppLogLevel
{
    Trace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    None = 5
}

public sealed class Logger : IDisposable
{
    private readonly object _sync = new();
    private readonly string _logDirectory;
    private readonly string _archiveDirectory;
    private bool _enabled = true;
    private bool _detailed;
    private AppLogLevel _minimumLevel = AppLogLevel.Information;

    public Logger()
    {
        _logDirectory = AppPaths.LogsDirectory;
        _archiveDirectory = Path.Combine(_logDirectory, "archive");
        Directory.CreateDirectory(_archiveDirectory);
        ArchiveCompletedWeeks();
    }

    public void Configure(AppSettings settings)
    {
        if (settings is null)
            return;

        _enabled = settings.EnableLogging;
        _detailed = settings.EnableDetailedLogging;
        _minimumLevel = ParseLevel(settings.LogLevel);

        Info($"Logging configured. enabled={_enabled} level={_minimumLevel} detailed={_detailed}");
    }

    public void Entry(
        string operation,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string member = "")
    {
        if (!_detailed)
            return;

        Write(AppLogLevel.Debug, $"ENTER {operation}", null, file, line, member);
    }

    public void Trace(
        string message,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string member = "")
    {
        if (!_detailed)
            return;

        Write(AppLogLevel.Trace, message, null, file, line, member);
    }

    public void Debug(
        string message,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string member = "")
    {
        if (!_detailed)
            return;

        Write(AppLogLevel.Debug, message, null, file, line, member);
    }

    public void Info(
        string message,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string member = "") =>
        Write(AppLogLevel.Information, message, null, file, line, member);

    public void Warn(
        string message,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string member = "") =>
        Write(AppLogLevel.Warning, message, null, file, line, member);

    public void Error(
        string message,
        Exception? ex = null,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string member = "") =>
        Write(AppLogLevel.Error, message, ex, file, line, member);

    public void Dispose()
    {
        // File.AppendAllText is used per write, so no stream remains open.
    }

    private void Write(AppLogLevel level, string message, Exception? ex, string file, int line, string member)
    {
        if (!_enabled || level < _minimumLevel || _minimumLevel == AppLogLevel.None)
            return;

        try
        {
            var now = DateTime.Now;
            var path = Path.Combine(_logDirectory, $"{now:yyyy-MM-dd}.log");
            var fileName = string.IsNullOrWhiteSpace(file) ? "unknown" : Path.GetFileName(file);
            var location = $"{fileName}:{line} {member}";
            var levelText = level switch
            {
                AppLogLevel.Information => "INFO",
                AppLogLevel.Warning => "WARN",
                AppLogLevel.Error => "ERROR",
                AppLogLevel.Debug => "DEBUG",
                _ => "TRACE"
            };

            var detail = ex is null
                ? message
                : $"{message} | {ex.GetType().FullName}: {ex.Message}{Environment.NewLine}{ex.StackTrace}";
            var lineText = $"{now:yy/MM/dd HH:mm:ss} [{levelText}] [{location}] {detail}{Environment.NewLine}";

            lock (_sync)
            {
                Directory.CreateDirectory(_logDirectory);
                File.AppendAllText(path, lineText);
            }
        }
        catch
        {
            // Logging must never terminate the tray application.
        }
    }

    private static AppLogLevel ParseLevel(string? value)
    {
        if (Enum.TryParse<AppLogLevel>(value, ignoreCase: true, out var parsed))
            return parsed;

        return AppLogLevel.Information;
    }

    private void ArchiveCompletedWeeks()
    {
        try
        {
            var today = DateTime.Today;
            var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
            var currentWeekMonday = today.AddDays(-daysSinceMonday);

            var logFiles = Directory.EnumerateFiles(_logDirectory, "????-??-??.log", SearchOption.TopDirectoryOnly)
                .Select(path => new { Path = path, Date = TryGetLogDate(path) })
                .Where(x => x.Date.HasValue && x.Date.Value < currentWeekMonday)
                .GroupBy(x => StartOfWeek(x.Date!.Value));

            foreach (var week in logFiles)
            {
                var weekStart = week.Key;
                var weekEnd = weekStart.AddDays(6);
                var archivePath = Path.Combine(_archiveDirectory, $"WinPicker_logs_{weekStart:yyyyMMdd}-{weekEnd:yyyyMMdd}.zip");

                using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Update);
                var existingNames = archive.Entries.Select(e => e.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var item in week.OrderBy(x => x.Date))
                {
                    var entryName = Path.GetFileName(item.Path);
                    if (!existingNames.Contains(entryName))
                        archive.CreateEntryFromFile(item.Path, entryName, CompressionLevel.Optimal);

                    File.Delete(item.Path);
                }
            }
        }
        catch
        {
            // Archiving is best-effort and must never prevent application startup.
        }
    }

    private static DateTime? TryGetLogDate(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return DateTime.TryParseExact(
            name,
            "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out var date)
            ? date.Date
            : null;
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.Date.AddDays(-daysSinceMonday);
    }
}
