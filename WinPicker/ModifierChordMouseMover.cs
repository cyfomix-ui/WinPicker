using System.Runtime.InteropServices;

namespace WinPicker;

internal sealed class ModifierChordMouseMover : IDisposable
{
    // v0.33:
    // Count Alt key releases, then wait for this decision window.
    // 1 tap: no action
    // 2 taps: move cursor to tray
    // 3+ taps: move cursor to tray and show WinPicker
    private const int AltTapDecisionWaitMilliseconds = 750;

    private readonly Action _onWinAltChord;
    private readonly Action _onAltDoubleTap;
    private readonly Action _onAltTripleTap;
    private readonly Action<int> _onWinAltItemHotkey;
    private readonly Action _onRightAltSpace;
    private readonly Action _onRightAltZ;
    private readonly Logger _logger;
    private readonly NativeMethods.LowLevelKeyboardProc _proc;
    private System.Windows.Forms.Timer? _winAltChordTimer;
    private System.Windows.Forms.Timer? _altTapDecisionTimer;
    private readonly SynchronizationContext? _syncContext;
    private readonly Thread _hookThread;
    private readonly ManualResetEventSlim _hookReady = new(false);
    private uint _hookThreadId;

    private IntPtr _hookHandle;
    private bool _leftWinDown;
    private bool _rightWinDown;
    private bool _leftAltDown;
    private bool _rightAltDown;
    private bool _winAltChordHandled;
    private bool _rightAltSpaceHandled;
    private bool _rightAltZHandled;
    private readonly HashSet<int> _handledWinAltItemKeys = new();
    private int _altTapCount;
    private bool _disposed;

    public ModifierChordMouseMover(
        Action onWinAltChord,
        Action onAltDoubleTap,
        Action onAltTripleTap,
        Action<int> onWinAltItemHotkey,
        Action onRightAltSpace,
        Action onRightAltZ,
        Logger logger)
    {
        _onWinAltChord = onWinAltChord;
        _onAltDoubleTap = onAltDoubleTap;
        _onAltTripleTap = onAltTripleTap;
        _onWinAltItemHotkey = onWinAltItemHotkey;
        _onRightAltSpace = onRightAltSpace;
        _onRightAltZ = onRightAltZ;
        _logger = logger;
        _syncContext = SynchronizationContext.Current;
        _proc = HookCallback;
        _hookThread = new Thread(HookThreadMain)
        {
            IsBackground = true,
            Name = "WinPicker low-level keyboard hook"
        };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();
        _hookReady.Wait(TimeSpan.FromSeconds(3));
    }

    private void HookThreadMain()
    {
        try
        {
            _hookThreadId = NativeMethods.GetCurrentThreadId();
            _winAltChordTimer = new System.Windows.Forms.Timer { Interval = 140 };
            _winAltChordTimer.Tick += OnWinAltChordTimerTick;
            _altTapDecisionTimer = new System.Windows.Forms.Timer { Interval = AltTapDecisionWaitMilliseconds };
            _altTapDecisionTimer.Tick += OnAltTapDecisionTimerTick;

            _hookHandle = NativeMethods.SetWindowsHookEx(
                NativeMethods.WH_KEYBOARD_LL,
                _proc,
                NativeMethods.GetModuleHandle(null),
                0);
            _hookReady.Set();

            if (_hookHandle == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                Post(() => _logger.Warn($"Modifier chord hook registration failed. Win32Error={error}"));
                return;
            }

            Post(() => _logger.Info("Modifier chord hook registered on dedicated STA thread."));
            Application.Run();
        }
        catch (Exception ex)
        {
            _hookReady.Set();
            Post(() => _logger.Error("Low-level keyboard hook thread failed.", ex));
        }
        finally
        {
            _winAltChordTimer?.Stop();
            _winAltChordTimer?.Dispose();
            _altTapDecisionTimer?.Stop();
            _altTapDecisionTimer?.Dispose();
            if (_hookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                var message = wParam.ToInt32();
                var key = Marshal.ReadInt32(lParam);
                var isKeyDown = message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN;
                var isKeyUp = message is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP;

                if (isKeyDown || isKeyUp)
                {
                    var handled = UpdateKeyState(key, isKeyDown);
                    if (handled)
                        return new IntPtr(1);
                }
            }
        }
        catch
        {
            // Never perform file I/O or blocking work inside the low-level hook callback.
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private bool UpdateKeyState(int key, bool isDown)
    {
        var winDownBeforeUpdate = _leftWinDown || _rightWinDown;
        var altDownBeforeUpdate = _leftAltDown || _rightAltDown;

        // v0.36: Win+Alt+item is handled here, not by the picker form.
        // This avoids ordinary Alt menu/system handling swallowing Alt+number.
        if (winDownBeforeUpdate && altDownBeforeUpdate && TryGetListIndexFromWinAltItemKey(key, out var itemIndex))
        {
            if (isDown)
            {
                if (_handledWinAltItemKeys.Add(key))
                {
                    CancelAltTapSequence();
                    _winAltChordTimer?.Stop();
                    _winAltChordHandled = true;
                    Post(() => _onWinAltItemHotkey(itemIndex));
                }

                return true;
            }

            _handledWinAltItemKeys.Remove(key);
            return true;
        }

        // RightAlt+Space and RightAlt+Z remain as right-side alternatives.
        if (_rightAltDown && isDown && key == NativeMethods.VK_SPACE && !_rightAltSpaceHandled)
        {
            CancelAltTapSequence();
            _rightAltSpaceHandled = true;
            Post(_onRightAltSpace);
            return true;
        }

        if (_rightAltDown && isDown && key == NativeMethods.VK_Z && !_rightAltZHandled)
        {
            CancelAltTapSequence();
            _rightAltZHandled = true;
            Post(_onRightAltZ);
            return true;
        }

        var isModifierKey = true;

        switch (key)
        {
            case NativeMethods.VK_LWIN:
                _leftWinDown = isDown;
                if (!isDown)
                    _handledWinAltItemKeys.Clear();
                CancelAltTapSequence();
                break;
            case NativeMethods.VK_RWIN:
                _rightWinDown = isDown;
                if (!isDown)
                    _handledWinAltItemKeys.Clear();
                CancelAltTapSequence();
                break;
            case NativeMethods.VK_MENU:
            case NativeMethods.VK_LMENU:
                if (_leftAltDown && !isDown)
                    RegisterAltTap(isLeftAlt: true);

                _leftAltDown = isDown;
                break;
            case NativeMethods.VK_RMENU:
                if (_rightAltDown && !isDown)
                    RegisterAltTap(isLeftAlt: false);

                _rightAltDown = isDown;
                if (!isDown)
                {
                    _rightAltSpaceHandled = false;
                    _rightAltZHandled = false;
                }
                break;
            default:
                isModifierKey = false;
                if (isDown)
                    CancelAltTapSequence();
                break;
        }

        var winDown = _leftWinDown || _rightWinDown;
        var altDown = _leftAltDown || _rightAltDown;

        UpdateWinAltChord(isDown, isModifierKey, winDown, altDown);
        return false;
    }

    private static bool TryGetListIndexFromWinAltItemKey(int key, out int index)
    {
        return WindowShortcutKey.TryGetIndexFromVirtualKey(key, out index);
    }

    private void RegisterAltTap(bool isLeftAlt)
    {
        // Ignore Alt taps that are part of Win+Alt or RightAlt+Space/Z style chords.
        if (_leftWinDown || _rightWinDown || _rightAltSpaceHandled || _rightAltZHandled)
        {
            CancelAltTapSequence();
            return;
        }

        _altTapCount++;


        // Restart the decision timer on every Alt release.
        // This guarantees the double-tap action does not run before the triple-tap window has expired.
        _altTapDecisionTimer?.Stop();
        _altTapDecisionTimer?.Start();
    }

    private void OnAltTapDecisionTimerTick(object? sender, EventArgs e)
    {
        _altTapDecisionTimer?.Stop();

        var count = _altTapCount;
        CancelAltTapSequence();

        if (count >= 3)
        {
            Post(_onAltTripleTap);
            return;
        }

        if (count == 2)
        {
            Post(_onAltDoubleTap);
            return;
        }

    }

    private void CancelAltTapSequence()
    {
        _altTapDecisionTimer?.Stop();
        _altTapCount = 0;
    }

    private void UpdateWinAltChord(bool isDown, bool isModifierKey, bool winDown, bool altDown)
    {
        if (!winDown || !altDown)
        {
            _winAltChordTimer?.Stop();
            _winAltChordHandled = false;
            return;
        }

        if (!isModifierKey && isDown)
        {
            _winAltChordTimer?.Stop();
            return;
        }

        if (!_winAltChordHandled && !(_winAltChordTimer?.Enabled ?? false))
            _winAltChordTimer?.Start();
    }

    private void OnWinAltChordTimerTick(object? sender, EventArgs e)
    {
        _winAltChordTimer?.Stop();

        if (_winAltChordHandled)
            return;

        var winDown = _leftWinDown || _rightWinDown;
        var altDown = _leftAltDown || _rightAltDown;
        if (!winDown || !altDown)
            return;

        _winAltChordHandled = true;
        Post(_onWinAltChord);
    }

    private void Post(Action action)
    {
        if (_syncContext is not null)
            _syncContext.Post(_ => action(), null);
        else
            action();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        try
        {
            if (_hookThreadId != 0)
                NativeMethods.PostThreadMessage(_hookThreadId, NativeMethods.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            if (_hookThread.IsAlive)
                _hookThread.Join(TimeSpan.FromSeconds(3));
            _hookReady.Dispose();
            _logger.Info("Modifier chord hook thread stopped.");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to stop modifier chord hook thread.", ex);
        }
    }
}
