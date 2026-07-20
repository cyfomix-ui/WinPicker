# WinPicker v0.71 execution model

## Threads

1. **Main WinForms UI thread**
   - Paints `MonitorMapForm` from cached bitmaps.
   - Handles normal UI events.
   - Applies completed window-enumeration snapshots.
   - Swaps a completed thumbnail into `WindowThumbnailCache` and invalidates only that mapped rectangle.

2. **Low-level keyboard hook STA thread**
   - Owns `WH_KEYBOARD_LL` and a WinForms message loop.
   - Maintains only modifier/tap state and posts actions to the UI context.
   - Stops through `WM_QUIT`, then unhooks in `finally`.

3. **Outside-click mouse-hook STA thread**
   - Exists only for a monitor-map form when outside-click closing is enabled.
   - Owns `WH_MOUSE_LL`; callback only tests the click position and posts a close request.

4. **Live thumbnail worker**
   - One Task per monitor-map session.
   - Captures one window at a time. No per-window threads.
   - Uses minimum refresh age, priority ordering, in-flight exclusion, and failure backoff.
   - Cancellation and map-session generation prevent stale results from reaching a closed/reopened map.

5. **Shortcut-overlay STA thread**
   - One thread owns all orange shortcut badge forms.
   - Receives immutable target snapshots and checks position/visibility every 350 ms.
   - All badges are closed before its application context exits.

6. **Media-detection worker**
   - Existing v0.69 non-overlapping background media enumeration remains unchanged.

## Thumbnail scheduling

- Worker count: **1**.
- Work is selected every ~100-120 ms rather than enqueueing every window at once.
- Per-HWND minimum capture age: ~950 ms.
- Priority: selected window, hovered window, larger mapped/desktop windows, then oldest capture.
- Failed `PrintWindow` captures keep the previous valid cache and back off for 3 seconds.
- No unconditional `CopyFromScreen` fallback.

## Bitmap ownership

- The capture worker exclusively owns the new bitmap while capturing/resizing.
- Ownership transfers through an asynchronous UI post.
- The map UI thread swaps it into `WindowThumbnailCache` and disposes the previous bitmap.
- A worker never draws or disposes a cached bitmap.

## Map-session shutdown

- `FormClosing` increments the generation, cancels the live worker, clears queued targets, and asks the overlay STA thread to close all badges.
- Late captures are disposed when their generation no longer matches.
- `OnFormClosed` repeats cleanup defensively and disposes the UI-owned cache.
