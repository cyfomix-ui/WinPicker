using System.Globalization;

namespace WinPicker;

public static class UiText
{
    public static bool IsJapanese =>
        CultureInfo.InstalledUICulture.TwoLetterISOLanguageName.Equals("ja", StringComparison.OrdinalIgnoreCase) ||
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ja", StringComparison.OrdinalIgnoreCase);

    public static string AppName => "WinPicker";
    public static string Version => "v0.18";
    public static string GitHubUrl => "https://github.com/cyfomix-ui/";

    public static string Show => IsJapanese ? "表示" : "Show";
    public static string RestoreLastMove => IsJapanese ? "直前の移動を戻す" : "Restore last move";
    public static string Settings => IsJapanese ? "設定" : "Settings";
    public static string About => IsJapanese ? "WinPickerについて" : "About WinPicker";
    public static string OpenLogsFolder => IsJapanese ? "ログフォルダを開く" : "Open logs folder";
    public static string Exit => IsJapanese ? "終了" : "Exit";
    public static string DisplayHotkeyLabel => IsJapanese ? "表示" : "Show";
    public static string RestoreHotkeyLabel => IsJapanese ? "戻す" : "Restore";
    public static string SettingsSaved => IsJapanese ? "設定を保存しました。" : "Settings saved.";
    public static string NoRestoreHistory => IsJapanese ? "戻せる移動履歴がありません。" : "No move history to restore.";
    public static string MinimizedTag => IsJapanese ? "[最小化]" : "[Min]";
    public static string MinimizedStatus => IsJapanese ? "最小化" : "minimized";

    public static string HeaderInstruction => IsJapanese
        ? "ウィンドウをクリックして呼び戻し。モニターを右クリックで移動先を設定。"
        : "Click a window to summon. Right-click a monitor to choose the summon target.";

    public static string InitialStatus => IsJapanese
        ? "左クリック: ウィンドウ呼び戻し   右クリック: 移動先モニター設定"
        : "Left click: summon window   Right click monitor: set target";

    public static string FooterHelp => IsJapanese
        ? "Esc: 閉じる   F5: 再読込   Tab/Shift+Tab/矢印: 選択   Ctrl+矢印: 移動   Ctrl+Alt+矢印: サイズ変更   Enter: 呼び戻し"
        : "Esc: close   F5: refresh   Tab/Shift+Tab/Arrows: select   Ctrl+Arrows: move   Ctrl+Alt+Arrows: resize   Enter: summon";

    public static string Monitor(int number) => IsJapanese ? $"モニター {number}" : $"Monitor {number}";
    public static string TargetTag => IsJapanese ? "対象" : "TARGET";
    public static string PrimaryTag => IsJapanese ? "メイン" : "PRIMARY";
    public static string WindowsListHeader(int count) => IsJapanese ? $"ウィンドウ ({count})" : $"Windows ({count})";
    public static string SetThisMonitorAsTarget => IsJapanese ? "このモニターに移動" : "Move to this monitor";
    public static string TargetInfo(int number, string deviceName) => IsJapanese ? $"移動先: モニター {number} / {deviceName}" : $"Target: Monitor {number} / {deviceName}";
    public static string TargetSet(int number) => IsJapanese ? $"移動先を設定: モニター {number}" : $"Target set: Monitor {number}";
    public static string SelectedStatus(string title) => IsJapanese ? $"選択: {title}   ダブルクリックまたはEnter: 呼び戻し" : $"Selected: {title}   Double-click or Enter: summon";
    public static string PickerMoved(bool fast) => IsJapanese ? (fast ? "ミニ画面を大きく移動" : "ミニ画面を移動") : (fast ? "Picker moved fast" : "Picker moved");
    public static string PickerSizeSaved(int width, int height) => IsJapanese ? $"ミニ画面サイズを保存: {width} x {height}" : $"Picker size saved: {width} x {height}";
    public static string WindowListFontSizeSaved(float size) => IsJapanese ? $"リスト文字サイズを保存: {size:0.0}" : $"Window list font size saved: {size:0.0}";

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
               $"Ctrl+mouse wheel on the list: change list font size";
    }
}
