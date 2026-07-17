namespace WinPicker;

public sealed class AppSettings
{
    // Screen.AllScreens is zero-based and does not necessarily match Windows display numbers.
    // TargetMonitorDeviceName is saved by right-clicking a monitor in the mini picker or from Settings.
    public int MainMonitorIndex { get; set; } = 0;
    public string? TargetMonitorDeviceName { get; set; }
    public bool UsePrimaryScreen { get; set; } = false;

    public string Hotkey { get; set; } = "Win+Alt+Space";
    public string RestoreHotkey { get; set; } = "Win+Alt+Z";
    public string PopupPlacementMode { get; set; } = "Cursor"; // Cursor / Primary / Target / SpecificMonitor
    public string? PopupMonitorDeviceName { get; set; }
    public bool MoveCursorToTrayOnHotkey { get; set; } = true;
    public bool PreferExactTrayIconPosition { get; set; } = true;
    public bool KeepPickerFocused { get; set; } = true;

    public bool KeepMaximized { get; set; } = true;
    public bool RestoreMinimized { get; set; } = true;
    public bool ClosePopupAfterSummon { get; set; } = true;
    public bool ClosePopupOnDeactivate { get; set; } = false;
    public bool ClosePopupOnOutsideClick { get; set; } = true;
    public int PopupWidth { get; set; } = 1120;
    public int PopupHeight { get; set; } = 560;
    public bool FlashWindowOnHover { get; set; } = false;
    public bool ShowBorderOnHover { get; set; } = true;
    public bool ShowWindowThumbnails { get; set; } = true;
    public bool ShowWindowTitlesInMap { get; set; } = false;
    public bool ShowWindowList { get; set; } = true;
    public bool EnableAltItemHotkeys { get; set; } = false;
    public bool EnableMonitorScreenSaver { get; set; } = false;
    public bool SuppressMonitorScreenSaverWhenMediaVisible { get; set; } = true;
    public bool ShowMonitorScreenSaverRemainingTime { get; set; } = false;
    public int MonitorScreenSaverIdleMinutes { get; set; } = 5;
    public Dictionary<string, int> MonitorScreenSaverIdleMinutesByMonitor { get; set; } = new();
    public Dictionary<string, string> MonitorScreenSaverKinds { get; set; } = new();
    public Dictionary<string, bool> MonitorScreenSaverRunEvenWhenMediaVisible { get; set; } = new();
    public string TapoControlUrl { get; set; } = "http://127.0.0.1:8900/api/power";
    public int MonitorPowerControlDelayMinutes { get; set; } = 5;
    public Dictionary<string, bool> MonitorPowerControlEnabled { get; set; } = new();
    public Dictionary<string, string> MonitorPowerControlIpByMonitor { get; set; } = new();
    public bool EnableLogging { get; set; } = true;
    public string LogLevel { get; set; } = "Information";
    public bool EnableDetailedLogging { get; set; } = false;
    public bool UseSummonSize { get; set; } = false;
    public int SummonWidth { get; set; } = 1920;
    public int SummonHeight { get; set; } = 1600;
    public int WindowListWidth { get; set; } = 300;
    public float WindowListFontSize { get; set; } = 9.0f;
    public List<string> ExcludeProcesses { get; set; } = new();
    public List<string> ExcludeWindowTitles { get; set; } = new();
}
