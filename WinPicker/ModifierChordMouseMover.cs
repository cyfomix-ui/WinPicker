using System.Runtime.InteropServices;

namespace WinPicker;

internal sealed class ModifierChordMouseMover : IDisposable
{
    private const int AltDoubleTapMilliseconds = 360;

    private readonly Action _onWinAltChord;
    private readonly Action _onAltDoubleTap;
    private readonly Action _onRightAltSpace;
    private readonly Action _onRightAltZ;
    private readonly Logger _logger;
    private readonly NativeMethods.LowLevelKeyboardProc _proc;
    private readonly System.Windows.Forms.Timer _winAltChordTimer;
    private readonly SynchronizationContext? _syncContext;

    private IntPtr _hookHandle;
    private bool _leftWinDown;
    private bool _rightWinDown;
    private bool _leftAltDown;
    private bool _rightAltDown;
    private bool _winAltChordHandled;
    private bool _rightAltSpaceHandled;
    private bool _rightAltZHandled;
    private int _lastLeftAltUpTick = -100000;
    private int _lastRightAltUpTick = -100000;
    private bool _disposed;

    public ModifierChordMouseMover(
        Action onWinAltChord,
        Action onAltDoubleTap,
        Action onRightAltSpace,
        Action onRightAltZ,
        Logger logger)
    {
        _onWinAltChord = onWinAltChord;
        _onAltDoubleTap = onAltDoubleTap;
        _onRightAltSpace = onRightAltSpace;
        _onRightAltZ = onRightAltZ;
        _logger = logger;
        _syncContext = SynchronizationContext.Current;
        _proc = HookCallback;

        _winAltChordTimer = new System.Windows.Forms.Timer { Interval = 140 };
        _winAltChordTimer.Tick += OnWinAltChordTimerTick;

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
        // RightAlt+Space and RightAlt+Z remain as right-side alternatives.
        // RightAlt alone no longer moves the cursor; Alt double-tap is used instead.
        if (_rightAltDown && isDown && key == NativeMethods.VK_SPACE && !_rightAltSpaceHandled)
        {
            _rightAltSpaceHandled = true;
            Post(_onRightAltSpace);
            return true;
        }

        if (_rightAltDown && isDown && key == NativeMethods.VK_Z && !_rightAltZHandled)
        {
            _rightAltZHandled = true;
            Post(_onRightAltZ);
            return true;
        }

        var isModifierKey = true;

        switch (key)
        {
            case NativeMethods.VK_LWIN:
                _leftWinDown = isDown;
                break;
            case NativeMethods.VK_RWIN:
                _rightWinDown = isDown;
                break;
            case NativeMethods.VK_MENU:
            case NativeMethods.VK_LMENU:
                if (_leftAltDown && !isDown)
                    DetectAltDoubleTap(isLeftAlt: true);

                _leftAltDown = isDown;
                break;
            case NativeMethods.VK_RMENU:
                if (_rightAltDown && !isDown)
                    DetectAltDoubleTap(isLeftAlt: false);

                _rightAltDown = isDown;
                if (!isDown)
                {
                    _rightAltSpaceHandled = false;
                    _rightAltZHandled = false;
                }
                break;
            default:
                isModifierKey = false;
                break;
        }

        var winDown = _leftWinDown || _rightWinDown;
        var altDown = _leftAltDown || _rightAltDown;

        UpdateWinAltChord(isDown, isModifierKey, winDown, altDown);
        return false;
    }

    private void DetectAltDoubleTap(bool isLeftAlt)
    {
        var now = Environment.TickCount;
        var last = isLeftAlt ? _lastLeftAltUpTick : _lastRightAltUpTick;

        if (unchecked(now - last) >= 0 && unchecked(now - last) <= AltDoubleTapMilliseconds)
        {
            if (isLeftAlt)
                _lastLeftAltUpTick = -100000;
            else
                _lastRightAltUpTick = -100000;

            _logger.Info(isLeftAlt ? "Left Alt double-tap detected." : "Right Alt double-tap detected.");
            Post(_onAltDoubleTap);
            return;
        }

        if (isLeftAlt)
            _lastLeftAltUpTick = now;
        else
            _lastRightAltUpTick = now;
    }

    private void UpdateWinAltChord(bool isDown, bool isModifierKey, bool winDown, bool altDown)
    {
        if (!winDown || !altDown)
        {
            _winAltChordTimer.Stop();
            _winAltChordHandled = false;
            return;
        }

        // Win+Alt should mean "move the mouse to the tray" only when it is held as a modifier-only chord.
        // If Space, Z, or another key follows immediately, the normal registered hotkey should handle that action.
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
