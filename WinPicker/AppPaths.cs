namespace WinPicker;

internal static class AppPaths
{
    public static string AppDataDirectory
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var path = Path.Combine(root, "Cyfomix", "WinPicker");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string SettingsPath => Path.Combine(AppDataDirectory, "appsettings.json");

    public static string LogsDirectory
    {
        get
        {
            var path = Path.Combine(AppDataDirectory, "logs");
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
