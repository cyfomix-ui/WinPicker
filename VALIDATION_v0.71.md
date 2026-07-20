# WinPicker v0.71 validation record

## Completed in the generation environment

- Compared v0.69 source paths for synchronous `PrintWindow`, `CopyFromScreen`, hook installation, and screen-saver animation timers.
- Removed thumbnail capture from `MonitorMapForm.OnPaint`.
- Verified live capture contains no `CopyFromScreen` fallback.
- Verified keyboard and map mouse hooks are installed on dedicated STA threads.
- Verified all orange badges are owned by one STA thread, not one thread per window.
- Verified source delimiter/string/comment balance for every `.cs` file.
- Verified project and XML versions are aligned at 0.71.
- Verified old `WindowThumbnailCache.GetThumbnail()` call sites no longer remain.

## Validation performed on Windows

- `dotnet build .\WinPicker.sln -c Release` completed with 0 warnings and 0 errors.

## Manual GUI checks still required

The following checks require an interactive Windows desktop and representative multi-monitor applications:

- Real `PrintWindow` behavior
- Low-level Windows hook operation
- Badge click-through/Z-order behavior
- Four requested before/after CPU measurements
- GDI object and handle measurements

Use `Build.ps1 -Publish` on Windows and `Measure-WinPickerCpu.ps1` for repeatable CPU measurements. The measurement script reports CPU-time delta, one-core percentage, all-CPU percentage, thread count, handle count, and private memory.
