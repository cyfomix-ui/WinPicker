# WinPicker v0.71 changed files

| File | Reason |
|---|---|
| `WinPicker/MonitorMapForm.cs` | Removed synchronous map enumeration/capture from the UI path; added map-session generation, live capture lifecycle, UI-thread cache swaps, partial invalidation, and desktop badge lifecycle. |
| `WinPicker/WindowThumbnailCache.cs` | Converted the cache to UI-thread-only `TryGet`/`Swap`/`Prune`; it no longer captures windows. |
| `WinPicker/WindowThumbnailCaptureWorker.cs` | Added background `PrintWindow` capture and CPU-lighter HighQualityBilinear scaling; no `CopyFromScreen` fallback. |
| `WinPicker/LiveThumbnailScheduler.cs` | Added one-worker scheduling, ~950 ms per-HWND minimum age, priority ordering, duplicate prevention, backpressure, cancellation, generation checks, and failure backoff. |
| `WinPicker/WindowShortcutKey.cs` | Centralized `1..9,a..z` mapping so the displayed badge and actual shortcut always use the same logic. |
| `WinPicker/WindowShortcutOverlayManager.cs` | Added one dedicated STA thread for all click-through orange badges and their position/visibility tracking. |
| `WinPicker/ModifierChordMouseMover.cs` | Moved `WH_KEYBOARD_LL` to a dedicated STA message-loop thread and removed logging/blocking work from the hook callback. |
| `WinPicker/PickerOutsideClickCloser.cs` | Moved the map-only `WH_MOUSE_LL` hook to a dedicated STA message-loop thread and removed logging/blocking work from the callback. |
| `WinPicker/ScreenSaverOverlayForm.cs` | Stopped the Black timer, slowed Lines/Bubbles, made movement elapsed-time based, avoided static RandomText paints, and replaced per-bubble GraphicsPath usage with ellipse drawing. |
| `WinPicker/NativeMethods.cs` | Added `IsWindow`, thread-ID and `PostThreadMessage` APIs, and `WM_QUIT` support for dedicated message loops. |
| `WinPicker/WinPicker.csproj` | Updated version to 0.71. |
| `WinPicker/VersionInfo.xml` | Updated displayed version to 0.71. |
| `WinPicker/README.md` | Documented v0.71 behavior and thread/bitmap ownership. |
| `Measure-WinPickerCpu.ps1` | Added repeatable `TotalProcessorTime` measurement for the four requested Windows test scenarios. |
| `ARCHITECTURE_v0.71.md` | Documents threads, start/stop conditions, scheduling, and ownership. |
| `VALIDATION_v0.71.md` | Records completed static checks and Windows-only checks. |
