using System.Text.Json;

namespace WinPicker;

public static class SettingsService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string SettingsPath => AppPaths.SettingsPath;
    private const string RegistryPath = @"Software\Cyfomix\WinPicker";

    public static AppSettings Load(Logger logger)
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                var settings = CreateDefault();
                ApplyRegistryTargetMonitor(settings, logger);
                Save(settings, logger);
                return settings;
            }

            var json = File.ReadAllText(SettingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, Options) ?? CreateDefault();
            EnsureDefaults(loaded);
            ApplyRegistryTargetMonitor(loaded, logger);
            return loaded;
        }
        catch (Exception ex)
        {
            logger.Error("Failed to load appsettings.json. Using defaults.", ex);
            return CreateDefault();
        }
    }

    public static void Save(AppSettings settings, Logger logger)
    {
        try
        {
            EnsureDefaults(settings);
            var json = JsonSerializer.Serialize(settings, Options);
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            logger.Error("Failed to save appsettings.json.", ex);
        }
    }

    private static AppSettings CreateDefault()
    {
        return new AppSettings
        {
            MainMonitorIndex = 0,
            TargetMonitorDeviceName = null,
            UsePrimaryScreen = false,
            Hotkey = "Win+Alt+Space",
            RestoreHotkey = "Win+Alt+Z",
            PopupPlacementMode = "Cursor",
            PopupMonitorDeviceName = null,
            MoveCursorToTrayOnHotkey = true,
            PreferExactTrayIconPosition = true,
            KeepPickerFocused = true,
            KeepMaximized = true,
            RestoreMinimized = true,
            ClosePopupAfterSummon = true,
            ClosePopupOnDeactivate = false,
            ClosePopupOnOutsideClick = true,
            PopupWidth = 1440,
            PopupHeight = 800,
            FlashWindowOnHover = false,
            ShowBorderOnHover = true,
            ShowWindowThumbnails = true,
            ShowWindowTitlesInMap = false,
            ShowWindowList = true,
            EnableAltItemHotkeys = false,
            EnableMonitorScreenSaver = false,
            SuppressMonitorScreenSaverWhenMediaVisible = true,
            MonitorScreenSaverIdleMinutes = 5,
            MonitorScreenSaverIdleMinutesByMonitor = new Dictionary<string, int>(),
            MonitorScreenSaverKinds = new Dictionary<string, string>(),
            MonitorScreenSaverRunEvenWhenMediaVisible = new Dictionary<string, bool>(),
            TapoControlUrl = "http://127.0.0.1:8900/api/power",
            MonitorPowerControlDelayMinutes = 5,
            MonitorPowerControlEnabled = new Dictionary<string, bool>(),
            MonitorPowerControlIpByMonitor = new Dictionary<string, string>(),
            UseSummonSize = false,
            SummonWidth = 1920,
            SummonHeight = 1600,
            WindowListWidth = 300,
            WindowListFontSize = 9.0f,
            ExcludeProcesses = new List<string>
            {
                "SearchHost",
                "ShellExperienceHost",
                "StartMenuExperienceHost",
                "TextInputHost",
                "WinPicker"
            },
            ExcludeWindowTitles = new List<string>
            {
                "Program Manager"
            }
        };
    }


    public static void SaveTargetMonitorToRegistry(AppSettings settings, Logger logger)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
            if (key is null)
                return;

            if (settings.UsePrimaryScreen)
            {
                key.SetValue("TargetMonitorMode", "Primary", RegistryValueKind.String);
                key.SetValue("TargetMonitorNumber", 0, RegistryValueKind.DWord);
                key.SetValue("TargetMonitorZeroBasedIndex", 0, RegistryValueKind.DWord);
                key.DeleteValue("TargetMonitorDeviceName", throwOnMissingValue: false);
                logger.Info("Registry target monitor saved: Primary");
                return;
            }

            var zeroBased = Math.Max(0, settings.MainMonitorIndex);
            key.SetValue("TargetMonitorMode", "Specific", RegistryValueKind.String);
            key.SetValue("TargetMonitorNumber", zeroBased + 1, RegistryValueKind.DWord);
            key.SetValue("TargetMonitorZeroBasedIndex", zeroBased, RegistryValueKind.DWord);
            if (!string.IsNullOrWhiteSpace(settings.TargetMonitorDeviceName))
                key.SetValue("TargetMonitorDeviceName", settings.TargetMonitorDeviceName, RegistryValueKind.String);
            else
                key.DeleteValue("TargetMonitorDeviceName", throwOnMissingValue: false);

            logger.Info($"Registry target monitor saved: number={zeroBased + 1} device=\"{settings.TargetMonitorDeviceName ?? ""}\"");
        }
        catch (Exception ex)
        {
            logger.Error("Failed to save target monitor to registry.", ex);
        }
    }

    private static void ApplyRegistryTargetMonitor(AppSettings settings, Logger logger)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            if (key is null)
                return;

            var mode = key.GetValue("TargetMonitorMode") as string;
            if (string.Equals(mode, "Primary", StringComparison.OrdinalIgnoreCase))
            {
                settings.UsePrimaryScreen = true;
                settings.TargetMonitorDeviceName = null;
                settings.MainMonitorIndex = 0;
                logger.Info("Registry target monitor loaded: Primary");
                return;
            }

            var screens = Screen.AllScreens;
            if (screens.Length == 0)
                return;

            var deviceName = key.GetValue("TargetMonitorDeviceName") as string;
            if (!string.IsNullOrWhiteSpace(deviceName))
            {
                for (var i = 0; i < screens.Length; i++)
                {
                    if (!string.Equals(screens[i].DeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    settings.UsePrimaryScreen = false;
                    settings.MainMonitorIndex = i;
                    settings.TargetMonitorDeviceName = screens[i].DeviceName;
                    logger.Info($"Registry target monitor loaded by device: number={i + 1} device=\"{screens[i].DeviceName}\"");
                    return;
                }
            }

            var zeroBased = ToIntOrNull(key.GetValue("TargetMonitorZeroBasedIndex"));
            if (zeroBased is null)
            {
                var oneBased = ToIntOrNull(key.GetValue("TargetMonitorNumber"));
                if (oneBased is not null && oneBased.Value > 0)
                    zeroBased = oneBased.Value - 1;
            }

            if (zeroBased is null)
                return;

            var index = Math.Clamp(zeroBased.Value, 0, screens.Length - 1);
            settings.UsePrimaryScreen = false;
            settings.MainMonitorIndex = index;
            settings.TargetMonitorDeviceName = screens[index].DeviceName;
            logger.Info($"Registry target monitor loaded by index: number={index + 1} device=\"{screens[index].DeviceName}\"");
        }
        catch (Exception ex)
        {
            logger.Error("Failed to load target monitor from registry.", ex);
        }
    }

    private static int? ToIntOrNull(object? value)
    {
        return value switch
        {
            int i => i,
            long l => (int)l,
            string s when int.TryParse(s, out var i) => i,
            _ => null
        };
    }

    private static void EnsureDefaults(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Hotkey))
            settings.Hotkey = "Win+Alt+Space";

        if (string.IsNullOrWhiteSpace(settings.RestoreHotkey))
            settings.RestoreHotkey = "Win+Alt+Z";

        // v0.5 default hotkeys changed from Ctrl+Alt to Win+Alt.
        // Existing user-edited values are preserved, but the old packaged defaults are migrated.
        if (settings.Hotkey.Equals("Ctrl+Alt+Space", StringComparison.OrdinalIgnoreCase))
            settings.Hotkey = "Win+Alt+Space";

        if (settings.RestoreHotkey.Equals("Ctrl+Alt+Z", StringComparison.OrdinalIgnoreCase))
            settings.RestoreHotkey = "Win+Alt+Z";

        if (string.IsNullOrWhiteSpace(settings.PopupPlacementMode))
            settings.PopupPlacementMode = "Cursor";


        // v0.8: widen the default picker because the right-side window list is useful.
        // Migrate the old packaged defaults, while preserving other user-edited sizes.
        if (settings.PopupWidth <= 300 || settings.PopupWidth == 860 || settings.PopupWidth == 1120)
            settings.PopupWidth = 1440;

        if (settings.PopupHeight <= 200 || settings.PopupHeight == 520 || settings.PopupHeight == 560)
            settings.PopupHeight = 800;

        if (settings.WindowListWidth < 180 || settings.WindowListWidth == 260)
            settings.WindowListWidth = 300;

        if (settings.WindowListFontSize < 7.0f || settings.WindowListFontSize > 22.0f)
            settings.WindowListFontSize = 9.0f;

        if (settings.SummonWidth < 320 || settings.SummonWidth > 16000)
            settings.SummonWidth = 1920;

        if (settings.SummonHeight < 200 || settings.SummonHeight > 16000)
            settings.SummonHeight = 1600;

        if (settings.MonitorScreenSaverIdleMinutes < 1 || settings.MonitorScreenSaverIdleMinutes > 240)
            settings.MonitorScreenSaverIdleMinutes = 5;

        settings.MonitorScreenSaverIdleMinutesByMonitor ??= new Dictionary<string, int>();
        foreach (var key in settings.MonitorScreenSaverIdleMinutesByMonitor.Keys.ToList())
        {
            var value = settings.MonitorScreenSaverIdleMinutesByMonitor[key];
            if (value < 0 || value > 240)
                settings.MonitorScreenSaverIdleMinutesByMonitor[key] = 0;
        }

        settings.MonitorScreenSaverKinds ??= new Dictionary<string, string>();
        settings.MonitorScreenSaverRunEvenWhenMediaVisible ??= new Dictionary<string, bool>();
        if (string.IsNullOrWhiteSpace(settings.TapoControlUrl))
            settings.TapoControlUrl = "http://127.0.0.1:8900/api/power";
        if (settings.MonitorPowerControlDelayMinutes < 0 || settings.MonitorPowerControlDelayMinutes > 240)
            settings.MonitorPowerControlDelayMinutes = 5;
        settings.MonitorPowerControlEnabled ??= new Dictionary<string, bool>();
        settings.MonitorPowerControlIpByMonitor ??= new Dictionary<string, string>();

        settings.ExcludeProcesses ??= new List<string>();
        settings.ExcludeWindowTitles ??= new List<string>();
    }
}
