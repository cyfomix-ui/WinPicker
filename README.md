# WinPicker v0.57

[English](#english)

## 日本語

WinPicker は、複数モニター上のウィンドウをミニマップから選び、指定したモニターへ呼び戻せる Windows 11 用タスクトレイユーティリティです。

### v0.33 から v0.57 の主な更新

- モニター単位のスクリーンセーバーを追加しました。待機時間を全体またはモニター別に設定でき、黒画面・時計・日付・時計と日付から表示を選べます。
- 動画・音声再生中のモニターではスクリーンセーバーを抑制するメディア判定を追加しました。
- スクリーンセーバー開始前の残り時間を表示するデバッグオプションを追加しました。
- Tapo 電源制御を追加しました。モニターごとにデバイス IP を設定し、スクリーンセーバー開始後の自動 OFF、操作再開時の ON、右クリックメニューからの手動 ON/OFF が行えます。
- ミニマップからウィンドウ単体、または指定モニター上のアプリをまとめて最小化・復元できるようになりました。
- ピッカー外をクリックしたときに自動で閉じる動作を追加しました。
- 起動時のスプラッシュ画面と、埋め込み XML を使った一元的なバージョン表示を追加しました。
- ウィンドウ移動・復元、最小化状態、モニター識別、設定保存の安定性を改善しました。
- 日本語・英語 UI、ダークテーマ、アイコンおよびブランド画像を更新しました。

### 主な機能

- `Win + Alt + Space` でピッカーを表示・非表示
- `Win + Alt + Z` で直前のウィンドウ移動を復元
- `Win + Alt + P` で全モニターのスクリーンショットを保存
- ミニマップと右側一覧からウィンドウを選択して移動
- ウィンドウ位置とデスクトップアイコン配置の保存・復元
- モニター別スクリーンセーバー、最小化、Tapo 電源制御
- 日本語 Windows では日本語、それ以外では英語 UI

### 必要環境とビルド

- Windows 11
- .NET 8
- Visual Studio 2022（`.NET デスクトップ開発` ワークロード推奨）

```powershell
dotnet build .\WinPicker.sln -c Release
dotnet publish .\WinPicker\WinPicker.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false
```

`Build.ps1 -NoRun` でも、クリーン後に自己完結型の単一 EXE を発行できます。設定とログは `%APPDATA%\Cyfomix\WinPicker` に保存されます。

## English

WinPicker is a Windows 11 task-tray utility for selecting windows from a multi-monitor minimap and bringing them to a chosen monitor.

### Highlights since v0.33

- Added per-monitor screen savers with global or per-display idle times and black, clock, date, or clock-and-date modes.
- Added media detection to suppress the saver on monitors playing video or audio.
- Added an optional debug countdown showing the remaining time before each saver starts.
- Added Tapo power control with per-monitor device IPs, delayed automatic power-off, wake power-on, and manual On/Off commands in the monitor menu.
- Added commands to minimize or restore one window or all applications on a selected monitor.
- Added automatic picker closing when the user clicks outside it.
- Added a startup splash screen and centralized version display backed by embedded XML metadata.
- Improved window move/restore behavior, minimized-state handling, monitor identification, and settings persistence.
- Updated Japanese/English UI text, dark styling, icons, and branding assets.

### Key features

- Show or hide the picker with `Win + Alt + Space`
- Restore the previous window move with `Win + Alt + Z`
- Capture all monitors with `Win + Alt + P`
- Select and move windows from the minimap or right-side list
- Save and restore window geometry and desktop icon layouts
- Per-monitor screen saver, minimize/restore, and Tapo power controls
- Japanese UI on Japanese Windows; English UI otherwise

### Requirements and build

- Windows 11
- .NET 8
- Visual Studio 2022 with the `.NET desktop development` workload recommended

```powershell
dotnet build .\WinPicker.sln -c Release
dotnet publish .\WinPicker\WinPicker.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false
```

You can also run `Build.ps1 -NoRun` to clean and publish a self-contained single EXE. Settings and logs are stored under `%APPDATA%\Cyfomix\WinPicker`.

See [WinPicker/README.md](WinPicker/README.md) for detailed controls and settings. See [ASSETS_LICENSE.md](ASSETS_LICENSE.md) for bundled asset terms.
