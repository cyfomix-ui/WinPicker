using System.Reflection;
using System.Xml.Linq;

namespace WinPicker;

public sealed record AppVersionInfo(string AppName, string Version)
{
    public string NormalizedVersion => Version.Trim().TrimStart('v', 'V');
    public string VersionWithV => $"V{NormalizedVersion}";
    public string VersionWithLowerV => $"v{NormalizedVersion}";
    public string AppTitle => $"{AppName} {VersionWithV}";
    public string TrayTooltip => $"{AppName} Ver {NormalizedVersion}";
}

public static class VersionInfoService
{
    private static readonly Lazy<AppVersionInfo> LazyCurrent = new(Load);

    public static AppVersionInfo Current => LazyCurrent.Value;

    private static AppVersionInfo Load()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly
                .GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith(".VersionInfo.xml", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(resourceName))
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream is not null)
                    return LoadFromStream(stream);
            }

            var localPath = Path.Combine(AppContext.BaseDirectory, "VersionInfo.xml");
            if (File.Exists(localPath))
            {
                using var stream = File.OpenRead(localPath);
                return LoadFromStream(stream);
            }
        }
        catch
        {
            // Fall through to safe default.
        }

        return new AppVersionInfo("WinPicker", "0.38");
    }

    private static AppVersionInfo LoadFromStream(Stream stream)
    {
        var doc = XDocument.Load(stream);
        var root = doc.Root;

        var appName = root?.Element("AppName")?.Value?.Trim();
        var version = root?.Element("Version")?.Value?.Trim();

        if (string.IsNullOrWhiteSpace(appName))
            appName = "WinPicker";

        if (string.IsNullOrWhiteSpace(version))
            version = "0.38";

        return new AppVersionInfo(appName, version);
    }
}
