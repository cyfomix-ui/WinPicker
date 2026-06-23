# WinPicker v0.18

WinPicker is a small Windows 11 task-tray utility for multi-monitor environments.
It shows a dark mini map of all monitors and visible windows. When the right-side list is enabled, window rectangles are thumbnail-first and the list shows window names. When the list is disabled, the mini map switches to text-only rectangles for readability. Click a window in the mini map to summon it to the configured target monitor.

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

The target monitor is saved by device name where possible, because Windows display numbers do not always match `Screen.AllScreens` order. v0.18 and later write the target monitor to `HKCU\Software\Cyfomix\WinPicker` and reads it back on startup when present.

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


## v0.18 changes

- When the right-side window list is disabled, mini-map window rectangles no longer show thumbnails; they show text labels only.
- UI language now follows the OS/UI culture: Japanese on Japanese Windows, English on other languages.
- `WinPickerについて` / `About WinPicker` also switches between Japanese and English.
- Added GitHub URL to the About dialog: https://github.com/cyfomix-ui/

## v0.18 changes

- Monitor labels are drawn outside monitor rectangles where possible, so they do not cover thumbnails.
- `Ctrl+Mouse wheel` over the right-side window list changes the list font size and saves it.
- The summon target monitor is written to the current-user registry key `HKCU\Software\Cyfomix\WinPicker`.
- On startup, the registry target monitor is used when available; otherwise WinPicker falls back to `appsettings.json` defaults.
- Packaged default picker size is now wider/taller for large multi-monitor layouts.

## v0.18 changes

- The mini-map window rectangles are now thumbnail-first; titles are hidden by default because the right-side list provides names.
- The default mini picker width is wider to make room for the window-name list.
- `Ctrl+Alt+Arrow` resizes the mini picker and saves the new size.
- The app now tries to move the cursor to the actual WinPicker tray icon using `Shell_NotifyIconGetRect`; if that fails, it falls back to the estimated tray area.
- Added a setting for preferring the exact WinPicker tray icon position.

## v0.18 changes

- Added best-effort preview images inside each mini-map window rectangle.
- Added an optional right-side window-name list.
- List selection and mini-map selection are linked.
- Added a custom purple WinPicker app icon and tray icon.
- Added settings for preview images and the window-name list.

## v0.18 changes

- The mini picker stays TOPMOST and keeps keyboard focus while it is visible.
- `Tab`, arrow keys, `Ctrl+Arrow`, and `Enter` remain usable after moving the selection.
- Pressing `Win+Alt+Space` moves the mouse cursor to the task tray area before showing the picker, so it opens from a stable location.
- `Esc` closes the picker and releases focus.

## v0.18 changes

- Default show hotkey changed to `Win+Alt+Space`.
- Default restore hotkey changed to `Win+Alt+Z`.
- `Ctrl+Arrow` moves the mini picker window after it is shown.
- Hover highlight no longer flashes the taskbar icon by default.


## v0.18

- Added Cyfomix about image to the About window.
- Added a small Cyfomix image before the WinPicker title in the picker header.
- Kept the existing tray icon unchanged.


## v0.18

- Fixed About window line breaks.
- Changed the About window to a dark theme.
- Added a best-effort dark title bar setting for the About window.


## v0.18 Single-file publish

v0.18 is prepared for a true single-file EXE publish.

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


## v0.18

- Reduced the white flash on picker startup.
- The picker starts at near-zero opacity and becomes visible after the first dark paint completes.
- The picker now overrides background painting to fill the client area with the dark color immediately.
- Added best-effort dark title bar on the picker window as well.


## v0.18

- Added Win+Alt modifier-only cursor move.
- Holding Win+Alt for a short moment moves the mouse cursor to the WinPicker tray icon or estimated tray area.
- Win+Alt+Space keeps the existing picker show/hide behavior.
- Win+Alt+Z keeps the existing restore-last-move behavior.
