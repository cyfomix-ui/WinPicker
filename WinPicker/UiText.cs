using System.Globalization;

namespace WinPicker;

public static class UiText
{
    public static bool IsJapanese =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ja", StringComparison.OrdinalIgnoreCase);

    public static string AppName => VersionInfoService.Current.AppName;
    public static string Version => VersionInfoService.Current.VersionWithLowerV;
    public static string AppTitleWithVersion => VersionInfoService.Current.AppTitle;
    public static string TrayTooltip => VersionInfoService.Current.TrayTooltip;
    public static string SplashStarting => IsJapanese ? "起動中..." : "Starting...";
    public static string SplashDescription => IsJapanese ? "ウィンドウを素早く呼び戻す準備をしています。" : "Preparing your window picker.";
    public static string GitHubUrl => "https://github.com/cyfomix-ui/";

    public static string Show => IsJapanese ? "表示" : "Show";
    public static string RestoreLastMove => IsJapanese ? "直前の移動を戻す" : "Restore last move";
    public static string Settings => IsJapanese ? "設定" : "Settings";
    public static string About => IsJapanese ? "WinPickerについて" : "About WinPicker";
    public static string OpenLogsFolder => IsJapanese ? "ログフォルダを開く" : "Open logs folder";
    public static string Exit => IsJapanese ? "終了" : "Exit";
    public static string DisplayHotkeyLabel => IsJapanese ? "表示" : "Show";
    public static string RestoreHotkeyLabel => IsJapanese ? "戻す" : "Restore";
    public static string ScreenshotHotkeyLabel => IsJapanese ? "スクリーンショット" : "Screenshot";
    public static string SettingsSaved => IsJapanese ? "設定を保存しました。" : "Settings saved.";
    public static string NoRestoreHistory => IsJapanese ? "戻せる移動履歴がありません。" : "No move history to restore.";
    public static string MinimizedTag => IsJapanese ? "[最小化]" : "[Min]";
    public static string MinimizedStatus => IsJapanese ? "最小化" : "minimized";
    public static string ElevatedTag => IsJapanese ? "[管理者]" : "[Admin]";
    public static string GeometrySaveTitle => IsJapanese ? "ジオメトリを保存" : "Save layout";
    public static string GeometryRestoreTitle => IsJapanese ? "ジオメトリを復元" : "Restore layout";
    public static string GeometryNamePrompt => IsJapanese ? "保存名を入力してください。空欄なら日時名で保存します。" : "Enter a name. Leave empty to use a timestamp.";
    public static string NoGeometrySnapshots => IsJapanese ? "保存されたジオメトリがありません。" : "No saved layouts.";
    public static string GeometrySaved(string name) => IsJapanese ? $"ジオメトリを保存: {name}" : $"Layout saved: {name}";
    public static string GeometryRestored(string name) => IsJapanese ? $"ジオメトリを復元: {name}" : $"Layout restored: {name}";
    public static string GeometryRestoreWindows => IsJapanese ? "ウィンドウ" : "Windows";
    public static string GeometryRestoreIcons => IsJapanese ? "アイコン" : "Desktop icons";
    public static string GeometryWindowsRestored(string name) => IsJapanese ? $"ウィンドウを復元: {name}" : $"Window layout restored: {name}";
    public static string GeometryIconsRestored(string name) => IsJapanese ? $"アイコン位置を復元: {name}" : $"Desktop icon layout restored: {name}";
    public static string ScreenshotSaved(string path) => IsJapanese ? $"画面キャプチャ保存: {path}" : $"Screenshot saved: {path}";
    public static string ScreenshotFailed => IsJapanese ? "スクリーンショット保存に失敗しました。" : "Screenshot failed.";
    public static string TooltipSaveLayout => IsJapanese ? "ジオメトリを保存" : "Save layout";
    public static string TooltipRestoreLayout => IsJapanese ? "ジオメトリを復元" : "Restore layout";
    public static string TooltipScreenshot => IsJapanese ? "全画面キャプチャ" : "Capture all screens";
    public static string TooltipSettings => IsJapanese ? "設定を開く" : "Open settings";

    public static string HeaderInstruction => IsJapanese
        ? "ウィンドウをクリックして呼び戻し。モニターを右クリックで移動先を設定。"
        : "Click a window to summon. Right-click a monitor to choose the summon target.";

    public static string InitialStatus => IsJapanese
        ? "左クリック: ウィンドウ呼び戻し   右クリック: 移動先モニター設定"
        : "Left click: summon window   Right click monitor: set target";

    public static string FooterHelp => IsJapanese
        ? "Esc: 閉じる   F5: 再読込   Tab/矢印: 選択   Enter: 呼び戻し   Win+Alt+項番: 移動/復元"
        : "Esc: close   F5: refresh   Tab/Arrows: select   Enter: summon   Win+Alt+item: move/restore";

    public static string SaverStatusOff => IsJapanese ? "セーバー Off" : "Saver Off";
    public static string SaverStatusActive(TimeSpan duration) => IsJapanese ? $"セーバー動作中: {FormatDuration(duration)}" : $"Saver active: {FormatDuration(duration)}";
    public static string SaverStatusUntil(TimeSpan remaining) => IsJapanese ? $"セーバーまで: {FormatDuration(remaining)}" : $"Until saver: {FormatDuration(remaining)}";
    public static string MonitorOffTimeOff => IsJapanese ? "MonitorOffTime Off" : "MonitorOffTime Off";
    public static string MonitorOffTimeDelay(int minutes) => IsJapanese ? $"MonitorOffTime +{minutes}分" : $"MonitorOffTime +{minutes}m";
    public static string SaverDismissedAndPowerOnSent(int monitorNumber) => IsJapanese
        ? $"モニター {monitorNumber}: セーバーを解除し、電源Onを送信しました。"
        : $"Monitor {monitorNumber}: saver dismissed and power-on sent.";
    public static string InvalidIpAddress => IsJapanese ? "有効なIPv4またはIPv6アドレスを入力してください。" : "Enter a valid IPv4 or IPv6 address.";
    public static string MonitorPowerOffCountdown(DateTime now, int minutes, int seconds) => IsJapanese
        ? $"{now:HH:mm}  モニターOffまで {minutes}:{seconds:00}"
        : $"{now:HH:mm}  Monitor off in {minutes}:{seconds:00}";

    private static string FormatDuration(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
            value = TimeSpan.Zero;

        return value.TotalHours >= 1
            ? $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}"
            : $"{Math.Max(0, (int)value.TotalMinutes)}:{value.Seconds:00}";
    }

    public static string Monitor(int number) => IsJapanese ? $"モニター {number}" : $"Monitor {number}";
    public static string TargetTag => IsJapanese ? "対象" : "TARGET";
    public static string PrimaryTag => IsJapanese ? "メイン" : "PRIMARY";
    public static string WindowsListHeader(int count) => IsJapanese ? $"ウィンドウ ({count})" : $"Windows ({count})";
    public static string SetThisMonitorAsTarget => IsJapanese ? "このモニターに移動" : "Move to this monitor";
    public static string MinimizeThisApp => IsJapanese ? "このアプリを最小化" : "Minimize this app";
    public static string RestoreThisApp => IsJapanese ? "このアプリを元に戻す" : "Restore this app";
    public static string MinimizeAppsOnThisMonitor => IsJapanese ? "このモニターのアプリを最小化" : "Minimize apps on this monitor";
    public static string RestoreAppsOnThisMonitor => IsJapanese ? "このモニターのアプリを元に戻す" : "Restore apps on this monitor";
    public static string AppMinimized(string title) => IsJapanese ? $"最小化: {title}" : $"Minimized: {title}";
    public static string AppRestored(string title) => IsJapanese ? $"元に戻す: {title}" : $"Restored: {title}";
    public static string MonitorAppsMinimized(int number, int count) => IsJapanese ? $"モニター {number} のアプリを {count} 件最小化" : $"Minimized {count} app(s) on monitor {number}";
    public static string MonitorAppsRestored(int number, int count) => IsJapanese ? $"モニター {number} のアプリを {count} 件元に戻す" : $"Restored {count} app(s) on monitor {number}";
    public static string SaverKindMenu => IsJapanese ? "セーバー種類" : "Saver type";
    public static string SaverKindOff => IsJapanese ? "なし" : "Off";
    public static string SaverKindBlack => IsJapanese ? "ブラック" : "Black";
    public static string SaverKindRandomText => IsJapanese ? "ランダム文字" : "Random text";
    public static string SaverKindLines => IsJapanese ? "ライン" : "Lines";
    public static string SaverKindBubbles => IsJapanese ? "バブル" : "Bubbles";
    public static string SaverKindSet(int number, string kindName) => IsJapanese ? $"モニター {number} のセーバー: {kindName}" : $"Monitor {number} saver: {kindName}";
    public static string SaverRunEvenWhenMediaVisible => IsJapanese ? "動画サービス稼働中でも起動" : "Start even when video service is active";
    public static string MonitorSaverIdleMenu => IsJapanese ? "このモニターの待機時間" : "Idle time for this monitor";
    public static string MonitorSaverIdleGlobal(int globalMinutes) => IsJapanese ? $"0分（全体設定: {globalMinutes}分）" : $"0 min (global: {globalMinutes} min)";
    public static string MonitorSaverIdleMinutesItem(int minutes) => IsJapanese ? $"{minutes}分" : $"{minutes} min";
    public static string MonitorSaverIdleCurrent(int monitorNumber, int minutes, int globalMinutes) => IsJapanese
        ? $"モニター {monitorNumber} の待機時間: {(minutes <= 0 ? $"全体設定({globalMinutes}分)" : $"{minutes}分")}"
        : $"Monitor {monitorNumber} idle time: {(minutes <= 0 ? $"global ({globalMinutes} min)" : $"{minutes} min")}";
    public static string SaverRunEvenWhenMediaVisibleSet(int number, bool enabled) => IsJapanese
        ? $"モニター {number}: 動画サービス稼働中でも起動 {(enabled ? "ON" : "OFF")}"
        : $"Monitor {number}: start even when video is active {(enabled ? "ON" : "OFF")}";
    public static string TargetInfo(int number, string deviceName) => IsJapanese ? $"移動先: モニター {number} / {deviceName}" : $"Target: Monitor {number} / {deviceName}";
    public static string TargetSet(int number) => IsJapanese ? $"移動先を設定: モニター {number}" : $"Target set: Monitor {number}";
    public static string SelectedStatus(string title) => IsJapanese ? $"選択: {title}   ダブルクリックまたはEnter: 呼び戻し" : $"Selected: {title}   Double-click or Enter: summon";
    public static string PickerMoved(bool fast) => IsJapanese ? (fast ? "ミニ画面を大きく移動" : "ミニ画面を移動") : (fast ? "Picker moved fast" : "Picker moved");
    public static string PickerSizeSaved(int width, int height) => IsJapanese ? $"ミニ画面サイズを保存: {width} x {height}" : $"Picker size saved: {width} x {height}";
    public static string WindowListFontSizeSaved(float size) => IsJapanese ? $"リスト文字サイズを保存: {size:0.0}" : $"Window list font size saved: {size:0.0}";
    public static string AltItemMoved(string title) => IsJapanese ? $"項番キーで移動: {title}" : $"Moved by item key: {title}";
    public static string AltItemRestored(string title) => IsJapanese ? $"項番キーで元の位置へ復元: {title}" : $"Restored by item key: {title}";
    public static string AltItemFailed(string title) => IsJapanese ? $"項番キー操作に失敗: {title}" : $"Item key action failed: {title}";
    public static string NoWindowRestoreHistory(string title) => IsJapanese ? $"このウィンドウの戻し履歴がありません: {title}" : $"No restore history for this window: {title}";

    public static string SettingsTitle => IsJapanese ? "WinPicker 設定" : "WinPicker Settings";
    public static string ShowHotkeySetting => IsJapanese ? "ミニ画面を表示するショートカット" : "Show picker hotkey";
    public static string RestoreHotkeySetting => IsJapanese ? "直前の移動を戻すショートカット" : "Restore last move hotkey";
    public static string TargetMonitorSetting => IsJapanese ? "ウィンドウの移動先モニター" : "Window summon target monitor";
    public static string PopupPlacementSetting => IsJapanese ? "ミニ画面の表示位置" : "Picker placement";
    public static string PopupMonitorSetting => IsJapanese ? "ミニ画面を出す指定モニター" : "Specific picker monitor";
    public static string WindowsPrimaryDisplay => IsJapanese ? "Windowsメインディスプレイ" : "Windows primary display";
    public static string PopupAtCursor => IsJapanese ? "マウス位置 / トレイ位置付近" : "Mouse / tray area";
    public static string PopupAtPrimary => IsJapanese ? "Windowsメインモニター中央" : "Center of Windows primary monitor";
    public static string PopupAtTarget => IsJapanese ? "移動先モニター中央" : "Center of target monitor";
    public static string PopupAtSpecific => IsJapanese ? "指定モニター中央" : "Center of specified monitor";
    public static string CloseAfterSummon => IsJapanese ? "ウィンドウを呼び戻した後、ミニ画面を閉じる" : "Close picker after summoning a window";
    public static string MoveCursorToTray => IsJapanese ? "ショートカット起動時、マウスカーソルをタスクトレイ付近へ移動する" : "Move mouse cursor near the task tray when opened by hotkey";
    public static string PreferExactTrayIcon => IsJapanese ? "可能ならWinPickerのトレイアイコン位置へマウスを移動する" : "Move mouse to the WinPicker tray icon when possible";
    public static string KeepPickerFocused => IsJapanese ? "ミニ画面表示中は最前面にしてキー操作を維持する" : "Keep picker topmost and keyboard-controllable";
    public static string ShowWindowThumbnails => IsJapanese ? "リスト表示中はミニ画面内のウィンドウ枠にプレビュー画像を表示する" : "Show window previews in the map when the window list is visible";
    public static string ShowWindowList => IsJapanese ? "右側にウィンドウ名リストを常時表示する" : "Always show the window title list on the right";
    public static string EnableAltItemHotkeys => IsJapanese ? "Win+Alt+項番キーでリストのウィンドウを移動 / 復元する" : "Use Win+Alt+item key to move / restore list windows";
    public static string EnableMonitorScreenSaver => IsJapanese ? "モニター毎スクリーンセーバーを有効にする" : "Enable per-monitor screen saver";
    public static string MonitorScreenSaverIdleMinutes => IsJapanese ? "セーバー待機時間（分）" : "Saver idle time (minutes)";
    public static string SuppressMonitorScreenSaverWhenMediaVisible => IsJapanese ? "動画 / YouTube らしいウィンドウがあるモニターでは起動しない" : "Do not start saver on monitors with likely video / YouTube windows";
    public static string ShowMonitorScreenSaverRemainingTime => IsJapanese ? "残時間を表示する" : "Show remaining saver time";
    public static string EnableLoggingSetting => IsJapanese ? "動作ログを出力する" : "Enable operation logging";
    public static string LogLevelSetting => IsJapanese ? "ログレベル" : "Log level";
    public static string EnableDetailedLoggingSetting => IsJapanese ? "詳細ログを出力する（関数入口や高頻度処理を含む）" : "Enable detailed logging (function entry and frequent operations)";
    public static string LogsArchiveHint => IsJapanese ? @"ログ: %APPDATA%\Cyfomix\WinPicker\logs（完了週は archive へ自動圧縮）" : @"Logs: %APPDATA%\Cyfomix\WinPicker\logs (completed weeks are archived automatically)";
    public static string TapoControlUrlSetting => IsJapanese ? "Tapo制御URL" : "Tapo control URL";
    public static string MonitorPowerControlDelayMinutesSetting => IsJapanese ? "モニター電源制御時間（セーバー開始後・分）" : "Monitor power delay after saver starts (minutes)";
    public static string InvalidTapoControlUrl => IsJapanese ? "Tapo制御URLには http:// または https:// で始まる有効なURLを指定してください。" : "Enter a valid Tapo control URL beginning with http:// or https://.";
    public static string InvalidMonitorPowerControlDelay => IsJapanese ? "モニター電源制御時間は0～240分で指定してください。" : "Monitor power delay must be between 0 and 240 minutes.";
    public static string MonitorPowerControlMenu => IsJapanese ? "このモニターの電源を制御する" : "Control power for this monitor";
    public static string MonitorPowerControlEnabled => IsJapanese ? "電源制御を有効にする" : "Enable power control";
    public static string MonitorPowerControlIp => IsJapanese ? "Tapo機器IP..." : "Tapo device IP...";
    public static string MonitorPowerControlIpUnset => IsJapanese ? "未設定" : "Not set";
    public static string MonitorPowerControlDialogTitle => IsJapanese ? "モニター電源制御" : "Monitor power control";
    public static string MonitorPowerControlDialogMessage => IsJapanese ? "このモニターに対応するTapo機器のIPアドレスを入力してください。" : "Enter the IP address of the Tapo device for this monitor.";
    public static string MonitorPowerControlStatus(int monitorNumber, bool enabled, string ip) => IsJapanese
        ? $"モニター{monitorNumber}: 電源制御={(enabled ? "有効" : "無効")} / IP={ip}"
        : $"Monitor {monitorNumber}: power control={(enabled ? "enabled" : "disabled")} / IP={ip}";
    public static string ManualMonitorPowerOn => IsJapanese ? "このモニターの電源をOnする" : "Turn this monitor power on";
    public static string ManualMonitorPowerOff => IsJapanese ? "このモニターの電源をOffする" : "Turn this monitor power off";
    public static string ManualMonitorPowerRequested(int monitorNumber, string state) => IsJapanese
        ? $"モニター{monitorNumber}: 電源{(state.Equals("on", StringComparison.OrdinalIgnoreCase) ? "On" : "Off")}を要求しました"
        : $"Monitor {monitorNumber}: power {(state.Equals("on", StringComparison.OrdinalIgnoreCase) ? "on" : "off")} requested";
    public static string ManualMonitorPowerUnavailable(int monitorNumber) => IsJapanese
        ? $"モニター{monitorNumber}: Tapo機器IPまたは制御URLが未設定です"
        : $"Monitor {monitorNumber}: Tapo device IP or control URL is not configured";
    public static string UseSummonSize => IsJapanese ? "移動時のウィンドウサイズを指定する" : "Use a fixed window size when moving";
    public static string SummonSizeSetting => IsJapanese ? "移動時サイズ" : "Move size";
    public static string SummonWidthSetting => IsJapanese ? "幅" : "Width";
    public static string SummonHeightSetting => IsJapanese ? "高さ" : "Height";
    public static string HotkeyExample => IsJapanese ? "例: Win+Alt+Space / Win+Alt+Z / Ctrl+Shift+F12" : "Examples: Win+Alt+Space / Win+Alt+Z / Ctrl+Shift+F12";
    public static string Save => IsJapanese ? "保存" : "Save";
    public static string Cancel => IsJapanese ? "キャンセル" : "Cancel";
    public static string HotkeysMustDiffer => IsJapanese ? "表示ショートカットと戻すショートカットは別にしてください。" : "Show and restore hotkeys must be different.";

    public static string InvalidHotkeyBalloon(string label, string hotkey) => IsJapanese
        ? $"{label}ショートカットが不正です: {hotkey}"
        : $"Invalid {label} hotkey: {hotkey}";

    public static string HotkeyRegistrationFailed(string label, string hotkey) => IsJapanese
        ? $"{label}ショートカット {hotkey} を登録できません。他のアプリが使用中かもしれません。"
        : $"Could not register {label} hotkey {hotkey}. Another app may already be using it.";

    public static string AboutText(AppSettings settings)
    {
        if (IsJapanese)
        {
            return $"WinPicker {Version}\n\n" +
                   $"複数モニター上のウィンドウをミニマップから選んで呼び戻す常駐ツールです。\n\n" +
                   $"GitHub: {GitHubUrl}\n\n" +
                   $"表示: {settings.Hotkey}\n" +
                   $"直前の移動を戻す: {settings.RestoreHotkey}\n\n" +
                   $"Esc: ミニ画面を閉じる\n" +
                   $"F5: 再読み込み\n" +
                   $"Tab / Shift+Tab / 矢印: 選択\n" +
                   $"Ctrl+矢印: ミニ画面移動\n" +
                   $"Ctrl+Alt+矢印: ミニ画面サイズ変更\n" +
                   $"Enter: 選択ウィンドウを呼び戻す\n" +
                   $"Win+Alt+項番: リストのウィンドウを移動/復元\n" +
                   $"Altを素早く2回: トレイへカーソル移動\n" +
                   $"Altを3回連打: ミニ画面表示\n" +
                   $"RightAlt+Space: ミニ画面表示\n" +
                   $"RightAlt+Z: 直前の移動を戻す\n" +
                   $"リスト上でCtrl+マウスホイール: リスト文字サイズ変更";
        }

        return $"WinPicker {Version}\n\n" +
               $"A tray utility that lets you pick and summon windows from a multi-monitor minimap.\n\n" +
               $"GitHub: {GitHubUrl}\n\n" +
               $"Show picker: {settings.Hotkey}\n" +
               $"Restore last move: {settings.RestoreHotkey}\n\n" +
               $"Esc: close picker\n" +
               $"F5: refresh\n" +
               $"Tab / Shift+Tab / Arrow keys: select\n" +
               $"Ctrl+Arrow keys: move picker\n" +
               $"Ctrl+Alt+Arrow keys: resize picker\n" +
               $"Enter: summon selected window\n" +
               $"Win+Win+Alt+item: move/restore a listed window\n" +
               $"Double-tap Alt: move cursor to tray\n" +
               $"Triple-tap Alt: show picker\n" +
               $"RightAlt+Space: show picker\n" +
               $"RightAlt+Z: restore last move\n" +
               $"Ctrl+mouse wheel on the list: change list font size";
    }
}
