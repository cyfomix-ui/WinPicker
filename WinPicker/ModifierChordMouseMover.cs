using System.Runtime.InteropServices;

namespace WinPicker;

internal sealed class ModifierChordMouseMover : IDisposable
{
    private const int ChordDelayMilliseconds = 140;

    private readonly Action _onWinAltChord;
    private readonly Logger _logger;
    private readonly NativeMethods.LowLevelKeyboardProc _proc;
    private readonly System.Windows.Forms.Timer _chordTimer;
    private IntPtr _hookHandle;
    private bool _leftWinDown;
    private bool _rightWinDown;
    private bool _leftAltDown;
    private bool _rightAltDown;
    private bool _winAltChordHandled;
    private bool _disposed;

    public ModifierChordMouseMover(Action onWinAltChord, Logger logger)
    {
        _onWinAltChord = onWinAltChord;
        _logger = logger;
        _proc = HookCallback;
        _chordTimer = new System.Windows.Forms.Timer { Interval = ChordDelayMilliseconds };
        _chordTimer.Tick += OnChordTimerTick;
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
                _logger.Warn($"Win+Alt mouse mover hook registration failed. Win32Error={win32Error}");
                return;
            }

            _logger.Info("Win+Alt mouse mover hook registered.");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to register Win+Alt mouse mover hook.", ex);
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
                    UpdateKeyState(key, isKeyDown);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Win+Alt mouse mover hook callback failed.", ex);
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private void UpdateKeyState(int key, bool isDown)
    {
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
                _leftAltDown = isDown;
                break;
            case NativeMethods.VK_RMENU:
                _rightAltDown = isDown;
                break;
            default:
                isModifierKey = false;
                break;
        }

        var winDown = _leftWinDown || _rightWinDown;
        var altDown = _leftAltDown || _rightAltDown;

        if (!winDown || !altDown)
        {
            _chordTimer.Stop();
            _winAltChordHandled = false;
            return;
        }

        // Win+Alt should mean "move the mouse to the tray" only when it is held as a modifier-only chord.
        // If Space, Z, or another key follows immediately, the normal registered hotkey should handle that action.
        if (!isModifierKey && isDown)
        {
            _chordTimer.Stop();
            return;
        }

        if (!_winAltChordHandled && !_chordTimer.Enabled)
            _chordTimer.Start();
    }

    private void OnChordTimerTick(object? sender, EventArgs e)
    {
        _chordTimer.Stop();

        if (_winAltChordHandled)
            return;

        var winDown = _leftWinDown || _rightWinDown;
        var altDown = _leftAltDown || _rightAltDown;
        if (!winDown || !altDown)
            return;

        _winAltChordHandled = true;
        _onWinAltChord();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            _chordTimer.Stop();
            _chordTimer.Dispose();

            if (_hookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
                _logger.Info("Win+Alt mouse mover hook unregistered.");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to unregister Win+Alt mouse mover hook.", ex);
        }
    }
}
