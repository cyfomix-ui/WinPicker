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
    private readonly System.Windows.Forms.Timer _winAltChordTimer;
    private readonly System.Windows.Forms.Timer _altTapDecisionTimer;
    private readonly SynchronizationContext? _syncContext;

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

        _winAltChordTimer = new System.Windows.Forms.Timer { Interval = 140 };
        _winAltChordTimer.Tick += OnWinAltChordTimerTick;

        _altTapDecisionTimer = new System.Windows.Forms.Timer { Interval = AltTapDecisionWaitMilliseconds };
        _altTapDecisionTimer.Tick += OnAltTapDecisionTimerTick;

        Install();
    }

    private void Install()
    {
        try
        {
            _hookHandle = NativeMethods.SetWindowsHookEx(
                NativeMethods.WH_KEYBOARD_LL,
                _proc,
                NativeMethods.GetModuleHandle(null),
                0);

            if (_hookHandle == IntPtr.Zero)
            {
                var win32Error = Marshal.GetLastWin32Error();
                _logger.Warn($"Modifier chord mouse mover hook registration failed. Win32Error={win32Error}");
                return;
            }

            _logger.Info("Modifier chord mouse mover hook registered.");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to register modifier chord mouse mover hook.", ex);
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
        catch (Exception ex)
        {
            _logger.Error("Modifier chord mouse mover hook callback failed.", ex);
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
                    _winAltChordTimer.Stop();
                    _winAltChordHandled = true;
                    _logger.Info($"Win+Alt item hotkey detected. key=0x{key:X} index={itemIndex}");
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
        index = -1;

        // Top-row 1-9
        if (key >= 0x31 && key <= 0x39)
        {
            index = key - 0x31;
            return true;
        }

        // Numpad 1-9
        if (key >= 0x61 && key <= 0x69)
        {
            index = key - 0x61;
            return true;
        }

        // A-Z for 10th and later entries
        if (key >= 0x41 && key <= 0x5A)
        {
            index = 9 + key - 0x41;
            return true;
        }

        return false;
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

        _logger.Info($"{(isLeftAlt ? "Left" : "Right")} Alt released. Tap count={_altTapCount}");

        // Restart the decision timer on every Alt release.
        // This guarantees the double-tap action does not run before the triple-tap window has expired.
        _altTapDecisionTimer.Stop();
        _altTapDecisionTimer.Start();
    }

    private void OnAltTapDecisionTimerTick(object? sender, EventArgs e)
    {
        _altTapDecisionTimer.Stop();

        var count = _altTapCount;
        CancelAltTapSequence();

        if (count >= 3)
        {
            _logger.Info($"Alt tap decision: {count} taps. Move cursor and show picker.");
            Post(_onAltTripleTap);
            return;
        }

        if (count == 2)
        {
            _logger.Info("Alt tap decision: 2 taps. Move cursor.");
            Post(_onAltDoubleTap);
            return;
        }

        _logger.Info($"Alt tap decision: {count} tap. No action.");
    }

    private void CancelAltTapSequence()
    {
        _altTapDecisionTimer.Stop();
        _altTapCount = 0;
    }

    private void UpdateWinAltChord(bool isDown, bool isModifierKey, bool winDown, bool altDown)
    {
        if (!winDown || !altDown)
        {
            _winAltChordTimer.Stop();
            _winAltChordHandled = false;
            return;
        }

        if (!isModifierKey && isDown)
        {
            _winAltChordTimer.Stop();
            return;
        }

        if (!_winAltChordHandled && !_winAltChordTimer.Enabled)
            _winAltChordTimer.Start();
    }

    private void OnWinAltChordTimerTick(object? sender, EventArgs e)
    {
        _winAltChordTimer.Stop();

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
            _winAltChordTimer.Stop();
            _winAltChordTimer.Dispose();

            _altTapDecisionTimer.Stop();
            _altTapDecisionTimer.Dispose();

            if (_hookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
                _logger.Info("Modifier chord mouse mover hook unregistered.");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to unregister modifier chord mouse mover hook.", ex);
        }
    }
}
