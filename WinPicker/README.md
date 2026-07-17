# WinPicker v0.68

## v0.61

- 廃止したカスタム待機時間ダイアログの旧ソース `MonitorIdleMinutesDialog.cs` が既存フォルダーに残っていても、`Build.ps1` が公開前に自動削除するよう修正しました。
- `Build.ps1 -Publish` の呼び出しを明示的に受け付けるようにしました。


- The per-monitor saver wait-time dialog now offers 90, 120, 150, and 180 minute choices, while still allowing direct numeric entry from 0 to 240 minutes.
- The pink monitor power-off countdown is explicitly kept above the screen-saver overlay.
- After the configured power-off delay expires, WinPicker repeats the Tapo Off request once per minute while that monitor's screen saver remains active. This provides retry behavior when the first request is missed or the device is temporarily unreachable.

# WinPicker v0.58

WinPicker is a small Windows 11 task-tray utility for multi-monitor environments.
It shows a dark mini map of all monitors and visible windows. When the right-side list is enabled, window rectangles are thumbnail-first and the list shows window names. When the list is disabled, the mini map switches to text-only rectangles for readability. Click a window in the mini map to summon it to the configured target monitor.

## v0.58 changes

- While the per-monitor screen saver is active and the debug remaining-time display is enabled, WinPicker now shows the remaining time until the configured Tapo monitor power-off action.
- The power-off countdown uses the same position and font as the saver countdown, with pink text: `current time  Monitor Off in m:ss`.
- The power-off countdown is shown only for monitors whose automatic power control is enabled and whose Tapo device IP/control URL are configured.

## v0.57 changes

- Opening the monitor map no longer pauses per-monitor screen saver management or closes active saver overlays.
- When a monitor has a Tapo device IP configured, its context menu now provides manual power On / Off commands below the media override item.
- Manual power commands reuse the configured Tapo control URL and send `ip=<device IP>&state=on|off`.

## v0.56 changes

- Added the `Show remaining saver time` debug option.
- Shows a transparent white Impact 14 pt countdown at the upper-right of each eligible monitor during the final 10 minutes before its screen saver starts.
- Countdown format is `current time  remaining`, for example `21:02  5:00`.
- The countdown closes on activity, media suppression, saver start, disabled saver mode, or monitor removal.

## v0.55 changes

- Fixed the settings layout so the monitor power delay label no longer overlaps its input field.

## v0.53 changes

- Added configurable `Tapo control URL` (default: `http://127.0.0.1:8900/api/power`).
- Added a global monitor power delay measured from the moment each monitor screen saver starts.
- Added per-monitor Tapo power control enable/disable and device IP settings to the monitor context menu.
- Sends `state=off` after the configured delay and `state=on` when mouse activity dismisses the saver on that monitor.
- HTTP requests are asynchronous, time out after five seconds, avoid duplicate requests, and log failures without stopping WinPicker.

## TapoCtrl integration

WinPicker sends monitor power commands through the `/api/power` endpoint provided by [TapoCtrl](https://github.com/cyfomix-ui/TapoCtrl). Configure a Tapo device IP for each monitor, then WinPicker can send `state=off` after the screen-saver delay and `state=on` when the saver is dismissed. The same endpoint is used by the monitor context menu's manual On/Off commands.

TapoCtrl defaults to `http://127.0.0.1:8080/api/power`, while WinPicker's bundled `appsettings.json` currently defaults to `http://127.0.0.1:8900/api/power`. Change either WinPicker's `Tapo control URL` or TapoCtrl's web-server port so they match. When TapoCtrl runs on another PC, use that PC's LAN address and configure its bind setting and firewall. Do not expose the unauthenticated API directly to an untrusted network.


## Requirements

- Windows 11
- Visual Studio 2022
- .NET 8 Desktop Runtime / SDK
- Workload: .NET desktop development

## Basic usage

1. Open `WinPicker.sln` in Visual Studio.
2. Press `F5`.
3. WinPicker starts in the task tray.
4. Left-click the tray icon, or press the show hotkey.
5. Click a mapped window to move it to the target monitor and bring it forward.

## Default shortcuts

- Show mini picker: `Win+Alt+Space`
- Restore the previous move: `Win+Alt+Z`

Both shortcuts can be changed from the tray menu:

`Right-click tray icon -> 設定`

## Mini picker controls

- `Esc`: close
- `F5`: refresh window list
- `Tab`: select next window
- `Shift+Tab`: select previous window
- Arrow keys: change selection
- `Ctrl+Arrow keys`: move the mini picker window by 40 px while keeping focus
- `Ctrl+Shift+Arrow keys`: move the mini picker window by 160 px while keeping focus
- `Ctrl+Alt+Left/Right`: shrink/enlarge the mini picker width and save the size
- `Ctrl+Alt+Up/Down`: shrink/enlarge the mini picker height and save the size
- `Ctrl+Alt+Shift+Arrow keys`: resize the mini picker faster and save the size
- `Enter`: summon selected window
- Left-click a window rectangle: summon that window
- Hover a window rectangle: highlight the real window with a border overlay
- Right-click a monitor: set it as the summon target
- If the window-name list is enabled, hover or click a row to select the same window in the mini map
- Double-click a row in the window-name list to summon it
- Mouse wheel over the window-name list: scroll the list
- `Ctrl+Mouse wheel` over the window-name list: enlarge/shrink the list font and save it

## Tray menu

Right-click the task tray icon to open:

- `表示`: show the mini picker
- `直前の移動を戻す`: restore the last moved window to its previous position
- `設定`: open the settings dialog
- `WinPickerについて`: show version/help information
- `ログフォルダを開く`: open the log folder
- `終了`: exit WinPicker

## Settings

The settings dialog can configure:

- Show hotkey
- Restore hotkey
- Target monitor for summoned windows
- Mini picker popup position
- Whether the mouse cursor moves to the tray area when the hotkey opens the picker
- Whether WinPicker tries to move the cursor to the exact WinPicker tray icon before falling back to the estimated tray area
- Whether the picker stays topmost and keeps keyboard focus
- Whether the picker closes after summoning a window
- Whether mini-map rectangles show captured window preview images
- Whether a right-side window-name list is always shown

The target monitor is saved by device name where possible, because Windows display numbers do not always match `Screen.AllScreens` order. v0.52 and later write the target monitor to `HKCU\Software\Cyfomix\WinPicker` and reads it back on startup when present.

## appsettings.json

WinPicker stores settings in `appsettings.json` next to the executable.

Important fields:

```json
{
  "Hotkey": "Win+Alt+Space",
  "RestoreHotkey": "Win+Alt+Z",
  "UsePrimaryScreen": false,
  "TargetMonitorDeviceName": null,
  "MainMonitorIndex": 0,
  "PopupPlacementMode": "Cursor",
  "PopupMonitorDeviceName": null,
  "MoveCursorToTrayOnHotkey": true,
  "PreferExactTrayIconPosition": true,
  "KeepPickerFocused": true,
  "ShowWindowThumbnails": true,
  "ShowWindowTitlesInMap": false,
  "ShowWindowList": true,
  "WindowListWidth": 300,
  "WindowListFontSize": 9.0
}
```

`PopupPlacementMode` values:

- `Cursor`: show near the mouse/tray position
- `Primary`: show centered on the Windows primary monitor
- `Target`: show centered on the summon target monitor
- `SpecificMonitor`: show centered on `PopupMonitorDeviceName`

## Notes

- The preview images are best-effort snapshots using Windows APIs. Some browser/GPU/video/protected windows may appear black or stale.
- Windows may block foreground activation in some situations. WinPicker uses a short TOPMOST pulse fallback.
- Administrator-elevated windows may require WinPicker to run as administrator.
- Fullscreen games and some special windows may not be movable.


## v0.52 changes

- When the right-side window list is disabled, mini-map window rectangles no longer show thumbnails; they show text labels only.
- UI language now follows the OS/UI culture: Japanese on Japanese Windows, English on other languages.
- `WinPickerについて` / `About WinPicker` also switches between Japanese and English.
- Added GitHub URL to the About dialog: https://github.com/cyfomix-ui/

## v0.52 changes

- Monitor labels are drawn outside monitor rectangles where possible, so they do not cover thumbnails.
- `Ctrl+Mouse wheel` over the right-side window list changes the list font size and saves it.
- The summon target monitor is written to the current-user registry key `HKCU\Software\Cyfomix\WinPicker`.
- On startup, the registry target monitor is used when available; otherwise WinPicker falls back to `appsettings.json` defaults.
- Packaged default picker size is now wider/taller for large multi-monitor layouts.

## v0.52 changes

- The mini-map window rectangles are now thumbnail-first; titles are hidden by default because the right-side list provides names.
- The default mini picker width is wider to make room for the window-name list.
- `Ctrl+Alt+Arrow` resizes the mini picker and saves the new size.
- The app now tries to move the cursor to the actual WinPicker tray icon using `Shell_NotifyIconGetRect`; if that fails, it falls back to the estimated tray area.
- Added a setting for preferring the exact WinPicker tray icon position.

## v0.52 changes

- Added best-effort preview images inside each mini-map window rectangle.
- Added an optional right-side window-name list.
- List selection and mini-map selection are linked.
- Added a custom purple WinPicker app icon and tray icon.
- Added settings for preview images and the window-name list.

## v0.52 changes

- The mini picker stays TOPMOST and keeps keyboard focus while it is visible.
- `Tab`, arrow keys, `Ctrl+Arrow`, and `Enter` remain usable after moving the selection.
- Pressing `Win+Alt+Space` moves the mouse cursor to the task tray area before showing the picker, so it opens from a stable location.
- `Esc` closes the picker and releases focus.

## v0.52 changes

- Default show hotkey changed to `Win+Alt+Space`.
- Default restore hotkey changed to `Win+Alt+Z`.
- `Ctrl+Arrow` moves the mini picker window after it is shown.
- Hover highlight no longer flashes the taskbar icon by default.


## v0.52

- Added Cyfomix about image to the About window.
- Added a small Cyfomix image before the WinPicker title in the picker header.
- Kept the existing tray icon unchanged.


## v0.52

- Fixed About window line breaks.
- Changed the About window to a dark theme.
- Added a best-effort dark title bar setting for the About window.


## v0.52 Single-file publish

v0.52 is prepared for a true single-file EXE publish.

Settings and logs are stored under:

```
%APPDATA%\Cyfomix\WinPicker
```

Publish command:

```powershell
dotnet publish .\WinPicker\WinPicker.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true
```

Output:

```
WinPicker\bin\Release\net8.0-windows\win-x64\publish\WinPicker.exe
```


## v0.52

- Reduced the white flash on picker startup.
- The picker starts at near-zero opacity and becomes visible after the first dark paint completes.
- The picker now overrides background painting to fill the client area with the dark color immediately.
- Added best-effort dark title bar on the picker window as well.


## v0.52

- Added Win+Alt modifier-only cursor move.
- Holding Win+Alt for a short moment moves the mouse cursor to the WinPicker tray icon or estimated tray area.
- Win+Alt+Space keeps the existing picker show/hide behavior.
- Win+Alt+Z keeps the existing restore-last-move behavior.


## v0.52

- Added a right-hand alternative shortcut family.
- `RightAlt` alone moves the mouse cursor to the task tray area.
- `RightAlt + Space` shows the picker.
- `RightAlt + Z` restores the last move.
- Note: most `Fn` keys are handled by keyboard hardware and are not exposed to Windows, so WinPicker uses `RightAlt` as the practical right-side alternative.


## v0.52

- Reduced monitor label font size to avoid overlapping text in dense multi-monitor layouts.
- Increased monitor label reserved band slightly.
- Changed the window-list header background to a soft purple.
- Clipped the window-list content area and reduced the visible row count to prevent partially drawn bottom text.


## v0.52

- Added Alt double-tap cursor movement:
  - Double-tap Left Alt: move cursor to tray
  - Double-tap Right Alt: move cursor to tray
- RightAlt+Space and RightAlt+Z remain available as right-side alternatives.
- RightAlt single press no longer moves the cursor.
- Added a stronger window move fallback path using SetWindowPos and MoveWindow.
- Added diagnostics when a window does not appear to move; elevated/admin apps may require running WinPicker as administrator.
- Removed the scroll range text from the window list header.
- Tightened the window-list drawing clip to prevent partial text at the bottom edge.


## v0.52

- Shows `[管理者]` / `[Admin]` on elevated windows when detectable.
- Added list-header icon buttons:
  - Save geometry
  - Restore geometry
  - Capture all screens
- Geometry snapshots store window placement and desktop icon positions together.
- Restore menu separates restore target into Windows and Icons.
- Screenshots are saved to `Pictures\WinPicker\yyyyMMdd_HHmmss.jpg`.
- Desktop icon layout restore is best-effort because it depends on Windows Explorer's desktop ListView internals.


## v0.52

- Moved the geometry/capture icon buttons above the window list frame.
- Increased icon button size.
- Clipped footer text so it does not draw under the right-side list.
- Camera capture now hides/closes the picker before saving the screenshot.
- Geometry name dialog OK/Cancel buttons now explicitly close the dialog.
- Tightened bottom list clipping to avoid partial stray text.


## v0.52

- Geometry save now saves immediately using a timestamp name. The name dialog was removed.
- Added global screenshot hotkey: `Win + Alt + P`.
- If the picker is open, `Win + Alt + P` hides/closes it before taking the screenshot.
- Added stronger cleanup for partial text leaking at the bottom of the window list.


## v0.52

- Fixed geometry restore menu colors.
  - Dark menu background uses white text.
  - Selected blue menu rows use black text.
- Applied the same dark menu renderer to nested submenus.
- Reviewed Japanese/English labels for the newer layout, screenshot, admin, and restore features.


## v0.52

- Added a settings gear icon next to the screenshot icon above the window list.
- Clicking the gear icon opens the WinPicker settings dialog from the picker window.


## v0.52

- When opening Settings from the picker gear icon, the picker temporarily disables TopMost and input capture.
- Settings dialog is shown as TopMost while open.
- After Settings closes, the picker restores its original TopMost state and refreshes the window list.


## v0.52

- When the picker gear icon is clicked, the picker window now hides and closes before opening Settings.
- This avoids the picker stealing focus from the Settings dialog.
- Settings dialog explicitly activates itself when shown.


## v0.52

- Shows the current version in the picker title/header, e.g. `WinPicker V0.29`.
- Swapped footer lines: selected window/status is now above in yellow, key help is below.
- Reduced footer font size for readability.
- Added hover tooltips for the list action icons.


## v0.52

- Added Alt triple-tap launcher.
- Double-tap Alt still moves the mouse cursor near the task tray.
- Triple-tap Alt moves the cursor near the task tray and shows the picker.


## v0.52

- Made Alt triple-tap easier to trigger by expanding the tap sequence window.
- Alt double-tap now waits briefly so a third tap can be recognized as triple-tap.
- Ensured the picker title/header uses the current version dynamically, e.g. `WinPicker V0.31`.


## v0.52

- Reworked Alt tap handling:
  - Waits for the Alt tap decision window.
  - 1 tap: no action.
  - 2 taps: move cursor to tray.
  - 3 or more taps: move cursor to tray and show WinPicker.
- Updated title/header to `WinPicker V0.32`.


## v0.52

- Alt tap handling changed to a delayed decision model:
  - 1 Alt tap: no action.
  - 2 Alt taps: move cursor to tray.
  - 3 or more Alt taps: move cursor to tray and show WinPicker.
- Alt taps are now counted on Alt key release.
- Title/header and assembly/file versions updated to v0.52.
- Added `Publish_WinPicker_v0_33.ps1` to stop old WinPicker processes, verify source version, clean bin/obj, and publish the correct EXE.


## v0.52

- Preserves actual Windows Z-order from `EnumWindows` for the list.
- Draws the minimap back-to-front so front windows are shown on top.
- Adds item keys to the window list:
  - 1-9 for the first 9 windows.
  - a-z for the 10th and later windows.
- Optional `Win+Alt+item` move/restore:
  - Enable it in Settings.
  - `Alt+5` moves item 5 to the target monitor.
  - If that window is already on the target monitor and has move history, the same key restores it.
- Optional fixed move size in Settings, such as 1920 x 1600.


## v0.52

- Fixed build error CS0121 in `MonitorMapForm.cs`.
- Removed unnecessary `Math.Ceiling()` around `TextRenderer.MeasureText(...).Width`.


## v0.52

- Changed item hotkeys from `Alt+item` to `Win+Alt+item`.
- Item hotkeys are now handled by the low-level keyboard hook, not by the picker form.
- This avoids ordinary Alt menu/system handling swallowing `Alt+number`.
- `Win+Alt+1` to `Win+Alt+9` target list items 1-9.
- `Win+Alt+A`, `Win+Alt+B`, ... target item 10 and later.


## v0.52

- Added outside-click close behavior for the picker panel.
- When the picker is visible, a mouse click outside the picker window closes it.
- Mouse movement alone does not close the picker.
- The context menu is ignored while open, so right-click monitor menus remain usable.


## v0.52

- Added XML-based version management with `VersionInfo.xml`.
- `UiText.AppName`, `UiText.Version`, title/header text, and tray tooltip now read from the XML version file.
- Tray hover text now shows application name and version, e.g. `WinPicker Ver 0.38`.
- Added `Build.ps1` to verify XML version binding before publishing.


## v0.52

- Renamed the version-specific publish helper to the generic `Build.ps1`.
- Future WinPicker source packages should use `Build.ps1`.
- `Build.ps1` stops old WinPicker processes, verifies `VersionInfo.xml`, cleans `bin` / `obj`, publishes win-x64 single EXE, and starts the published EXE unless `-NoRun` is specified.


## v0.52

- Replaced WinPicker branding assets with the new uploaded purple WinPicker icon.
- Updated `App.ico`, `AppIcon.png`, `CyfomixAbout.png`, `CyfomixHeader.png`, and added `WinPickerSplash.png`.
- The task tray icon, EXE icon, picker window icon, header image, About image, and splash image now use the same icon family.
- Added a startup splash screen with the new icon on the left and the current app/version text on the right.


## v0.52

- Added lightweight per-monitor pseudo screen saver.
- Global settings:
  - Enable / disable per-monitor saver.
  - Shared idle time in minutes for all monitors.
- Per-monitor setting:
  - Right-click a monitor in the WinPicker map.
  - Choose `Saver type`.
  - Available types: Off, Black, Random text, Lines.
- The saver is shown only on monitors where the mouse has not entered/moved for the configured idle time.
- Moving the mouse into a monitor closes that monitor's saver.
- WinPicker panel visibility pauses saver activation.
- The manager uses one 1-second timer, and animation timers run only while a saver overlay is visible.


## v0.52

- Improved the Random text monitor saver.
- Text is now displayed at random positions inside the target monitor.
- It randomly alternates between:
  - `WinPicker Ver x.xx` + current time
  - current date + current time
- Text color and size are randomized on each update.


## v0.52

- Random text saver:
  - Text now stays at one random position for about 15 seconds.
  - It fades out before moving to the next position.
  - The second line is center-aligned.
- Added a new `Bubbles` monitor saver type with lightweight circular drawing.


## v0.52

- Added optional media-window suppression for the per-monitor screen saver.
- New setting:
  - Do not start saver on monitors with likely video / YouTube windows.
- Detection is intentionally lightweight and heuristic:
  - Checks only every 5 seconds.
  - Detects common video player processes such as VLC, mpv, MPC, PotPlayer, DropMp4, and MvView.
  - Detects browser windows whose title includes YouTube, Netflix, Twitch, Prime Video, and similar hints.
- When a media-like window is found on a monitor, that monitor is treated as active and its saver is closed / not started.


## v0.52

- Added a per-monitor override in the monitor right-click menu:
  - `動画サービス稼働中でも起動`
  - `Start even when video service is active`
- Default is unchecked for every monitor.
- When unchecked, the global media suppression setting prevents the saver from starting on monitors with likely video / YouTube windows.
- When checked for a monitor, that monitor's saver can start even if video / YouTube-like windows are detected there.


## v0.52

- Random text saver:
  - Text now stays at one random position for about 30 seconds.
  - The existing fade-out behavior before moving is preserved.
- Includes the v0.52 per-monitor media override:
  - `動画サービス稼働中でも起動`


## v0.52

- Added window / monitor minimize commands to the WinPicker map right-click menu.
- When right-clicking on a mapped window:
  - `このアプリを最小化` / `Minimize this app` is shown at the top.
- When right-clicking a monitor:
  - `このモニターに移動`
  - `このモニターのアプリを最小化`
  - separator
  - saver settings
- `このモニターのアプリを最小化` minimizes visible windows that are mostly on that monitor.


## v0.52

- The right-click minimize menu now behaves as a toggle when possible.
- If an app was minimized from the WinPicker map menu, the app menu changes to `このアプリを元に戻す`.
- If monitor apps were minimized from the monitor menu, the monitor menu changes to `このモニターのアプリを元に戻す`.
- Restore history is kept while WinPicker is running, even if the picker panel is closed and opened again.


## v0.52

- Performance tuning for the WinPicker map panel.
- Thumbnails are no longer cleared on every window refresh; stale entries are pruned only when windows disappear.
- Thumbnail capture is budgeted to a few windows per paint so opening the panel remains responsive even with many windows.
- Mouse hover handling now avoids repeated repaint / focus / highlight work when staying on the same item.
- Tooltip updates are cached to avoid needless SetToolTip calls during mouse movement.
- Media suppression logging was reduced to avoid repeated log writes every few seconds.
- Failed thumbnail captures are cooled down briefly to avoid retry/repaint loops.


## v0.52

- Restored full thumbnail capture behavior for the monitor map.
- Removed the v0.52 per-paint thumbnail capture limit that could leave many windows as blue placeholders.
- Kept the lighter mouse-hover handling from v0.52 where possible.
- Window contents should appear in the map again as before.


## v0.52

- Added per-monitor idle time for the monitor screen saver.
- Right-click a monitor and open `このモニターの待機時間`.
- `0分` means the monitor follows the global idle time from Settings.
- Non-zero values override the global idle time only for that monitor.
- Added `TVTest.exe` / `TVTest` to the media-window suppression detection list.


## v0.52

- Reduced idle CPU usage for the per-monitor screen saver.
- When the per-monitor screen saver is disabled, its monitor timer is stopped.
- When enabled, the lightweight mouse / monitor check still runs once per second.
- Media / YouTube / TVTest window enumeration now runs at most once every 10 seconds.
- Media detection is skipped until a monitor is near its saver deadline, or while a saver is active and may need to be closed by media detection.

## v0.60

- Added 90, 120, 150, and 180 minute choices directly to the per-monitor idle-time submenu.
- Removed the custom idle-time input dialog.



## v0.64
- `MonitorDisplayStatus` の電源Off待機時間を nullable `TimeSpan?` として明示し、CS0173ビルドエラーを修正しました。

## v0.63
- モニターマップの各モニター外周ラベルに、`Saver active: xx` / `Until saver: yy` / `Saver Off` を表示します。
- 併せて `MonitorOffTime: zz` を表示し、Tapo電源制御が有効なモニターでは設定中のOff待機時間、未設定時は `Off` を表示します。
- スクリーンセーバー稼働中のモニターで、ウィンドウがない空き領域をダブルクリックすると、そのモニターのスクリーンセーバーを解除し、TapoのOnコマンドを送信します。

## v0.62
- モニターマップ表示時、個別スクリーンセーバー稼働中のモニターに薄い半透明の網掛けを表示します。表示のみで、クリック・右クリック・ドラッグなどの操作には影響しません。

## v0.65
- モニター外周ラベルを2段表示へ整理しました。
- 1段目は `モニターN  \\.\DISPLAYx`、2段目は `Until saver / Saver active / Saver Off` と `MonitorOffTime` を同じ行に表示します。


## v0.66
- セーバー開始時にモニター電源Off予定時刻を確定し、予定到達後にTapo Offを即送信します。失敗時はセーバー稼働中に1分ごとに再送します。
- Tapo Off要求が成功したモニターは、マップ表示を `MonitorOffTime Off` に切り替えます。

## v0.67
- 日別動作ログを `%APPDATA%\Cyfomix\WinPicker\logs` に出力します。
- ログ行は `YY/MM/DD HH:mm:ss`、レベル、呼出元ファイル名・行番号・関数名、動作内容の順で記録します。
- 設定画面にログ有効化、ログレベル、詳細ログのスイッチを追加しました。
- 詳細ログでは関数入口や1秒監視など高頻度処理も記録します。
- 完了した週の日別ログは起動時に `logs\archive` へ週単位ZIPで自動アーカイブします。
- 例外発生時は例外型、メッセージ、スタックトレースを記録します。


## v0.68
- UI language detection now follows the active Windows UI culture only. Non-Japanese OS/UI modes use English even when Japanese language resources are installed.
- Localized remaining hard-coded monitor saver, Tapo IP validation, power countdown, and saver-dismiss status strings.
