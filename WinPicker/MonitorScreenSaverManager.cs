namespace WinPicker;

public sealed class MonitorScreenSaverManager : IDisposable
{
    private static readonly TimeSpan MediaCheckInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MediaCheckLookAhead = TimeSpan.FromSeconds(12);

    private readonly AppSettings _settings;
    private readonly Logger _logger;
    private readonly Func<bool> _isPaused;
    private readonly WindowEnumerator _windowEnumerator;
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly Dictionary<string, DateTime> _lastActivity = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ScreenSaverOverlayForm> _active = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MonitorSaverCountdownForm> _countdowns = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _mediaSuppressedScreens = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _saverStartedUtc = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _powerOffDueUtc = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _poweredOffScreens = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _powerRequestsInFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _lastPowerAttemptUtc = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _desiredPowerState = new(StringComparer.OrdinalIgnoreCase);
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    private Point _lastCursor = Point.Empty;
    private string? _lastCursorScreenKey;
    private DateTime _lastMediaCheckUtc = DateTime.MinValue;
    private bool _disposed;

    public MonitorScreenSaverManager(AppSettings settings, Logger logger, Func<bool> isPaused)
    {
        _settings = settings;
        _logger = logger;
        _isPaused = isPaused;
        _windowEnumerator = new WindowEnumerator(settings, logger);

        _lastCursor = Cursor.Position;
        ResetAllActivity();

        _timer.Interval = 1000;
        _timer.Tick += (_, _) => Tick();

        if (_settings.EnableMonitorScreenSaver)
            StartTimer();
        else
            _logger.Info("Monitor screen saver manager initialized with timer stopped because saver is disabled.");
    }

    public bool IsSaverActive(Screen screen)
    {
        if (screen is null)
            return false;

        var key = GetScreenKey(screen);
        return _active.TryGetValue(key, out var form) && !form.IsDisposed && form.Visible;
    }

    public sealed record MonitorDisplayStatus(
        bool SaverConfigured,
        bool SaverActive,
        TimeSpan? SaverActiveDuration,
        TimeSpan? UntilSaver,
        bool PowerControlConfigured,
        bool MonitorPowerOff,
        TimeSpan? MonitorOffDelay);

    public MonitorDisplayStatus GetDisplayStatus(Screen screen)
    {
        if (screen is null)
            return new MonitorDisplayStatus(false, false, null, null, false, false, null);

        var key = GetScreenKey(screen);
        var now = DateTime.UtcNow;
        var saverConfigured = _settings.EnableMonitorScreenSaver && !string.Equals(GetSaverKind(key), "Off", StringComparison.OrdinalIgnoreCase);
        var saverActive = IsSaverActive(screen);
        TimeSpan? saverActiveDuration = null;
        TimeSpan? untilSaver = null;

        if (saverConfigured)
        {
            if (saverActive && _saverStartedUtc.TryGetValue(key, out var startedUtc))
                saverActiveDuration = now - startedUtc;
            else
            {
                if (!_lastActivity.TryGetValue(key, out var lastActivity))
                    lastActivity = now;

                var remaining = GetIdleTimeForScreen(key) - (now - lastActivity);
                if (remaining < TimeSpan.Zero)
                    remaining = TimeSpan.Zero;
                untilSaver = remaining;
            }
        }

        var powerConfigured = IsPowerControlConfigured(key);
        var monitorPowerOff = _poweredOffScreens.Contains(key);
        TimeSpan? powerDelay = powerConfigured && !monitorPowerOff
            ? TimeSpan.FromMinutes(Math.Clamp(_settings.MonitorPowerControlDelayMinutes, 0, 240))
            : null;

        return new MonitorDisplayStatus(
            saverConfigured,
            saverActive,
            saverActiveDuration,
            untilSaver,
            powerConfigured,
            monitorPowerOff,
            powerDelay);
    }

    public void DismissSaver(Screen screen, bool requestPowerOn)
    {
        if (screen is null)
            return;

        var key = GetScreenKey(screen);
        CloseForScreen(key);
        _lastActivity[key] = DateTime.UtcNow;

        if (requestPowerOn && HasPowerEndpoint(key))
            QueuePowerRequest(key, "on", requirePowerControlEnabled: false);
    }

    private void StartTimer()
    {
        if (_disposed || _timer.Enabled)
            return;

        ResetAllActivity();
        _lastMediaCheckUtc = DateTime.MinValue;
        _timer.Start();
        _logger.Info("Monitor screen saver timer started.");
    }

    private void StopTimer()
    {
        if (!_timer.Enabled)
            return;

        _timer.Stop();
        CloseAll();
        _mediaSuppressedScreens.Clear();
        _logger.Info("Monitor screen saver timer stopped.");
    }

    private void Tick()
    {
        _logger.Entry("Monitor screen saver timer tick");
        try
        {
            if (!_settings.EnableMonitorScreenSaver)
            {
                StopTimer();
                return;
            }

            if (_isPaused())
            {
                CloseAll();
                ResetAllActivity();
                return;
            }

            var screens = Screen.AllScreens;
            if (screens.Length == 0)
                return;

            var now = DateTime.UtcNow;
            var cursor = Cursor.Position;
            var cursorScreen = Screen.FromPoint(cursor);
            var cursorKey = GetScreenKey(cursorScreen);
            var cursorMoved = cursor != _lastCursor || !string.Equals(cursorKey, _lastCursorScreenKey, StringComparison.OrdinalIgnoreCase);

            if (cursorMoved)
            {
                _lastActivity[cursorKey] = now;
                CloseForScreen(cursorKey);
                _lastCursor = cursor;
                _lastCursorScreenKey = cursorKey;
            }

            var mediaCheckNeeded = IsMediaCheckNeeded(screens, cursorKey, now);
            if (mediaCheckNeeded)
                UpdateMediaSuppressionIfNeeded(screens, now);
            else if (!_settings.SuppressMonitorScreenSaverWhenMediaVisible && _mediaSuppressedScreens.Count > 0)
                _mediaSuppressedScreens.Clear();

            foreach (var screen in screens)
            {
                var key = GetScreenKey(screen);
                if (!_lastActivity.ContainsKey(key))
                    _lastActivity[key] = now;

                if (string.Equals(key, cursorKey, StringComparison.OrdinalIgnoreCase))
                    continue;

                var kind = GetSaverKind(key);
                if (kind == "Off")
                {
                    CloseForScreen(key);
                    CloseCountdownForScreen(key);
                    continue;
                }

                var idle = GetIdleTimeForScreen(key);
                var idleElapsed = now - _lastActivity[key];

                if (_mediaSuppressedScreens.Contains(key) && !ShouldRunEvenWhenMediaVisible(key))
                {
                    // Treat visible media as activity for that monitor unless the monitor-specific override is enabled.
                    _lastActivity[key] = now;
                    CloseForScreen(key);
                    CloseCountdownForScreen(key);
                    continue;
                }

                if (_active.ContainsKey(key))
                {
                    UpdatePowerOffCountdownForScreen(screen, key, now);
                    TryPowerOffAfterDelay(key, now);
                    continue;
                }

                var remaining = idle - idleElapsed;
                if (idleElapsed >= idle)
                {
                    CloseCountdownForScreen(key);
                    ShowForScreen(screen, key, kind);
                }
                else
                {
                    UpdateCountdownForScreen(screen, key, remaining);
                }
            }

            CloseRemovedScreens(screens);
        }
        catch (Exception ex)
        {
            _logger.Error("Monitor screen saver tick failed.", ex);
        }
    }

    private bool IsMediaCheckNeeded(Screen[] screens, string cursorKey, DateTime now)
    {
        if (!_settings.SuppressMonitorScreenSaverWhenMediaVisible)
            return false;

        if (now - _lastMediaCheckUtc < MediaCheckInterval)
            return false;

        foreach (var screen in screens)
        {
            var key = GetScreenKey(screen);
            if (string.Equals(key, cursorKey, StringComparison.OrdinalIgnoreCase))
                continue;

            if (ShouldRunEvenWhenMediaVisible(key))
                continue;

            var kind = GetSaverKind(key);
            if (kind == "Off")
                continue;

            if (!_lastActivity.TryGetValue(key, out var lastActivity))
                return true;

            var idle = GetIdleTimeForScreen(key);
            var elapsed = now - lastActivity;

            // Enumerate windows only when a monitor is near its saver deadline,
            // or if a saver is already active and may need to be closed by media detection.
            if (idle - elapsed <= MediaCheckLookAhead || _active.ContainsKey(key))
                return true;
        }

        return false;
    }

    public void ApplySettings()
    {
        try
        {
            CloseAll();
            ResetAllActivity();
            _mediaSuppressedScreens.Clear();
            _lastMediaCheckUtc = DateTime.MinValue;

            if (_settings.EnableMonitorScreenSaver)
                StartTimer();
            else
                StopTimer();
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to apply monitor screen saver settings.", ex);
        }
    }

    public static string GetScreenKey(Screen screen)
    {
        return string.IsNullOrWhiteSpace(screen.DeviceName) ? screen.Bounds.ToString() : screen.DeviceName;
    }

    private TimeSpan GetIdleTimeForScreen(string screenKey)
    {
        _settings.MonitorScreenSaverIdleMinutesByMonitor ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var minutes = 0;
        if (_settings.MonitorScreenSaverIdleMinutesByMonitor.TryGetValue(screenKey, out var monitorMinutes))
            minutes = monitorMinutes;

        if (minutes <= 0)
            minutes = _settings.MonitorScreenSaverIdleMinutes;

        minutes = Math.Clamp(minutes, 1, 240);
        return TimeSpan.FromMinutes(minutes);
    }

    private string GetSaverKind(string screenKey)
    {
        _settings.MonitorScreenSaverKinds ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (_settings.MonitorScreenSaverKinds.TryGetValue(screenKey, out var kind))
            return ScreenSaverOverlayForm.NormalizeKind(kind);

        return "Black";
    }

    private void UpdateMediaSuppressionIfNeeded(Screen[] screens, DateTime now)
    {
        _lastMediaCheckUtc = now;

        if (!_settings.SuppressMonitorScreenSaverWhenMediaVisible)
        {
            if (_mediaSuppressedScreens.Count > 0)
                _mediaSuppressedScreens.Clear();
            return;
        }

        _mediaSuppressedScreens.Clear();

        try
        {
            foreach (var window in _windowEnumerator.Enumerate())
            {
                if (window.IsMinimized || !IsLikelyMediaWindow(window))
                    continue;

                foreach (var screen in screens)
                {
                    if (!WindowTouchesScreenEnough(window.Bounds, screen.Bounds))
                        continue;

                    var key = GetScreenKey(screen);
                    _mediaSuppressedScreens.Add(key);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Media window detection failed: {ex.Message}");
        }
    }

    private static bool WindowTouchesScreenEnough(Rectangle window, Rectangle screen)
    {
        var intersection = Rectangle.Intersect(window, screen);
        if (intersection.Width <= 0 || intersection.Height <= 0)
            return false;

        var intersectionArea = (double)intersection.Width * intersection.Height;
        var screenArea = Math.Max(1.0, (double)screen.Width * screen.Height);
        var windowArea = Math.Max(1.0, (double)window.Width * window.Height);

        // A media/player window should either cover a meaningful part of the screen
        // or have most of itself on that screen.
        return intersectionArea / screenArea >= 0.12 || intersectionArea / windowArea >= 0.65;
    }

    private static bool IsLikelyMediaWindow(WindowInfo window)
    {
        var process = (window.ProcessName ?? string.Empty).Trim().ToLowerInvariant();
        var title = (window.Title ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(process) && string.IsNullOrWhiteSpace(title))
            return false;

        var playerProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "vlc",
            "mpv",
            "mpvnet",
            "ffplay",
            "wmplayer",
            "potplayer",
            "potplayermini",
            "potplayermini64",
            "mpc-hc",
            "mpc-hc64",
            "mpc-be",
            "mpc-be64",
            "movies",
            "video.ui",
            "dropmp4",
            "mvview",
            "mvhover",
            "mvexphover",
            "tvtest",
            "tvtest.exe"
        };

        if (playerProcesses.Contains(process))
            return true;

        var titleMediaHints = new[]
        {
            ".mp4", ".mkv", ".mov", ".avi", ".wmv", ".webm", ".m4v", ".ts", ".m2ts",
            "youtube", "youtu.be", "netflix", "prime video", "amazon prime", "twitch",
            "abema", "tver", "hulu", "disney+", "niconico", "ニコニコ", "動画", "video"
        };

        if (titleMediaHints.Any(h => title.Contains(h, StringComparison.OrdinalIgnoreCase)))
            return true;

        var browserProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "chrome",
            "msedge",
            "firefox",
            "brave",
            "opera",
            "vivaldi",
            "chromium"
        };

        if (browserProcesses.Contains(process))
        {
            // Browser suppression is title based. This avoids suppressing the saver just because
            // a browser exists on that monitor.
            return titleMediaHints.Any(h => title.Contains(h, StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }

    private bool ShouldRunEvenWhenMediaVisible(string screenKey)
    {
        _settings.MonitorScreenSaverRunEvenWhenMediaVisible ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        return _settings.MonitorScreenSaverRunEvenWhenMediaVisible.TryGetValue(screenKey, out var enabled) && enabled;
    }

    private void ShowForScreen(Screen screen, string key, string kind)
    {
        _logger.Entry($"Show saver. screen={key} kind={kind}");
        try
        {
            if (_active.ContainsKey(key))
                return;

            var form = new ScreenSaverOverlayForm(screen, kind);
            form.FormClosed += (_, _) =>
            {
                _active.Remove(key);
                _saverStartedUtc.Remove(key);
                _powerOffDueUtc.Remove(key);
            };
            _active[key] = form;
            var startedUtc = DateTime.UtcNow;
            _saverStartedUtc[key] = startedUtc;
            _poweredOffScreens.Remove(key);
            _lastPowerAttemptUtc.Remove(key);
            _desiredPowerState.Remove(key);

            if (IsPowerControlConfigured(key))
            {
                var delay = TimeSpan.FromMinutes(Math.Clamp(_settings.MonitorPowerControlDelayMinutes, 0, 240));
                _powerOffDueUtc[key] = startedUtc + delay;
                _logger.Info($"Monitor power-off scheduled. screen=\"{key}\" dueUtc={_powerOffDueUtc[key]:O} delayMinutes={delay.TotalMinutes:0}");
            }
            else
            {
                _powerOffDueUtc.Remove(key);
            }

            form.Show();

            _logger.Info($"Monitor screen saver shown. screen=\"{key}\" kind={kind}");
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to show monitor screen saver. screen=\"{key}\" kind={kind}", ex);
        }
    }

    private void CloseForScreen(string key)
    {
        _logger.Entry($"Close saver. screen={key}");
        CloseCountdownForScreen(key);

        if (_poweredOffScreens.Contains(key))
            QueuePowerRequest(key, "on");

        _saverStartedUtc.Remove(key);
        _powerOffDueUtc.Remove(key);
        _lastPowerAttemptUtc.Remove(key);
        if (!_active.TryGetValue(key, out var form))
            return;

        try
        {
            _active.Remove(key);
            if (!form.IsDisposed)
                form.Close();
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to close monitor screen saver. screen=\"{key}\"", ex);
        }
    }

    private void UpdateCountdownForScreen(Screen screen, string key, TimeSpan remaining)
    {
        if (!_settings.ShowMonitorScreenSaverRemainingTime || remaining <= TimeSpan.Zero || remaining > TimeSpan.FromMinutes(10))
        {
            CloseCountdownForScreen(key);
            return;
        }

        try
        {
            if (!_countdowns.TryGetValue(key, out var form) || form.IsDisposed)
            {
                form = new MonitorSaverCountdownForm(screen);
                form.FormClosed += (_, _) => _countdowns.Remove(key);
                _countdowns[key] = form;
                form.Show();
            }

            form.UpdateDisplay(remaining);
            form.EnsureVisibleAboveSaver();
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to show monitor saver countdown. screen=\"{key}\"", ex);
            CloseCountdownForScreen(key);
        }
    }

    private void UpdatePowerOffCountdownForScreen(Screen screen, string key, DateTime now)
    {
        if (!_settings.ShowMonitorScreenSaverRemainingTime ||
            !IsPowerControlConfigured(key) ||
            _poweredOffScreens.Contains(key) ||
            !_powerOffDueUtc.TryGetValue(key, out var dueUtc))
        {
            CloseCountdownForScreen(key);
            return;
        }

        var remaining = dueUtc - now;
        if (remaining <= TimeSpan.Zero)
        {
            CloseCountdownForScreen(key);
            return;
        }

        try
        {
            if (!_countdowns.TryGetValue(key, out var form) || form.IsDisposed)
            {
                form = new MonitorSaverCountdownForm(screen);
                form.FormClosed += (_, _) => _countdowns.Remove(key);
                _countdowns[key] = form;
                form.Show();
            }

            form.UpdatePowerOffDisplay(remaining);
            form.EnsureVisibleAboveSaver();
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to show monitor power-off countdown. screen=\"{key}\"", ex);
            CloseCountdownForScreen(key);
        }
    }

    private void CloseCountdownForScreen(string key)
    {
        if (!_countdowns.TryGetValue(key, out var form))
            return;

        try
        {
            _countdowns.Remove(key);
            if (!form.IsDisposed)
                form.Close();
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to close monitor saver countdown. screen=\"{key}\"", ex);
        }
    }

    private void TryPowerOffAfterDelay(string key, DateTime now)
    {
        if (!IsPowerControlConfigured(key))
            return;

        if (!_powerOffDueUtc.TryGetValue(key, out var dueUtc))
        {
            if (!_saverStartedUtc.TryGetValue(key, out var startedUtc))
                return;

            var delay = TimeSpan.FromMinutes(Math.Clamp(_settings.MonitorPowerControlDelayMinutes, 0, 240));
            dueUtc = startedUtc + delay;
            _powerOffDueUtc[key] = dueUtc;
        }

        if (now >= dueUtc)
            QueuePowerRequest(key, "off");
    }

    private bool IsPowerControlConfigured(string key)
    {
        _settings.MonitorPowerControlEnabled ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        return _settings.MonitorPowerControlEnabled.TryGetValue(key, out var enabled) && enabled && HasPowerEndpoint(key);
    }

    private bool HasPowerEndpoint(string key)
    {
        _settings.MonitorPowerControlIpByMonitor ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        return _settings.MonitorPowerControlIpByMonitor.TryGetValue(key, out var ip) && !string.IsNullOrWhiteSpace(ip) &&
               !string.IsNullOrWhiteSpace(_settings.TapoControlUrl);
    }

    public bool RequestPowerState(Screen screen, string state)
    {
        if (_disposed || screen is null)
            return false;

        var normalizedState = (state ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedState is not ("on" or "off"))
            return false;

        var key = GetScreenKey(screen);
        if (!HasPowerEndpoint(key))
            return false;

        QueuePowerRequest(key, normalizedState, requirePowerControlEnabled: false);
        return true;
    }

    private void QueuePowerRequest(string key, string state, bool requirePowerControlEnabled = true)
    {
        _logger.Entry($"Queue power request. screen={key} state={state}");
        if (_disposed)
            return;

        _desiredPowerState[key] = state;
        if (_powerRequestsInFlight.Contains(key))
            return;

        var endpointAvailable = HasPowerEndpoint(key);
        if ((state == "off" && requirePowerControlEnabled && !IsPowerControlConfigured(key)) || !endpointAvailable)
        {
            if (state == "on" && !endpointAvailable)
                _poweredOffScreens.Remove(key);
            return;
        }

        var now = DateTime.UtcNow;
        // While the screen saver remains active, repeat the Off command once per minute.
        // This also recovers from a missed request or a device that was temporarily unreachable.
        if (state == "off" && _lastPowerAttemptUtc.TryGetValue(key, out var lastAttempt) && now - lastAttempt < TimeSpan.FromMinutes(1))
            return;

        _lastPowerAttemptUtc[key] = now;
        _powerRequestsInFlight.Add(key);
        _ = SendPowerRequestAsync(key, state);
    }

    private async Task SendPowerRequestAsync(string key, string state)
    {
        _logger.Entry($"Send power request. screen={key} state={state}");
        try
        {
            _settings.MonitorPowerControlIpByMonitor ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!_settings.MonitorPowerControlIpByMonitor.TryGetValue(key, out var ip) || string.IsNullOrWhiteSpace(ip))
                return;

            var url = BuildPowerControlUrl(_settings.TapoControlUrl, ip.Trim(), state);
            using var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.Warn($"Tapo power request failed. screen=\"{key}\" state={state} status={(int)response.StatusCode}");
                return;
            }

            if (state == "off")
            {
                _poweredOffScreens.Add(key);
                CloseCountdownForScreen(key);
            }
            else
            {
                _poweredOffScreens.Remove(key);
            }

            _logger.Info($"Tapo power request succeeded. screen=\"{key}\" ip=\"{ip}\" state={state}");
        }
        catch (Exception ex)
        {
            _logger.Warn($"Tapo power request failed. screen=\"{key}\" state={state} error={ex.Message}");
        }
        finally
        {
            _powerRequestsInFlight.Remove(key);
            if (_desiredPowerState.TryGetValue(key, out var desired) && !string.Equals(desired, state, StringComparison.OrdinalIgnoreCase))
                QueuePowerRequest(key, desired, requirePowerControlEnabled: false);
        }
    }

    private static string BuildPowerControlUrl(string baseUrl, string ip, string state)
    {
        var trimmed = (baseUrl ?? string.Empty).Trim().TrimEnd('?', '&');
        var separator = trimmed.Contains('?') ? '&' : '?';
        return $"{trimmed}{separator}ip={Uri.EscapeDataString(ip)}&state={Uri.EscapeDataString(state)}";
    }

    private void CloseAll()
    {
        foreach (var key in _active.Keys.ToList())
            CloseForScreen(key);
        foreach (var key in _countdowns.Keys.ToList())
            CloseCountdownForScreen(key);
    }

    private void CloseRemovedScreens(Screen[] screens)
    {
        var keys = screens.Select(GetScreenKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var key in _active.Keys.ToList())
        {
            if (!keys.Contains(key))
                CloseForScreen(key);
        }
        foreach (var key in _countdowns.Keys.ToList())
        {
            if (!keys.Contains(key))
                CloseCountdownForScreen(key);
        }
    }

    private void ResetAllActivity()
    {
        var now = DateTime.UtcNow;
        foreach (var screen in Screen.AllScreens)
            _lastActivity[GetScreenKey(screen)] = now;

        _lastCursor = Cursor.Position;
        _lastCursorScreenKey = GetScreenKey(Screen.FromPoint(_lastCursor));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _timer.Stop();
        _timer.Dispose();
        foreach (var form in _active.Values.ToList())
        {
            try
            {
                if (!form.IsDisposed)
                    form.Close();
            }
            catch
            {
                // Application shutdown: do not send power-on commands or block exit.
            }
        }
        _active.Clear();
        foreach (var form in _countdowns.Values.ToList())
        {
            try
            {
                if (!form.IsDisposed)
                    form.Close();
            }
            catch
            {
                // Application shutdown.
            }
        }
        _countdowns.Clear();
        _saverStartedUtc.Clear();
        _httpClient.Dispose();

        _logger.Info("Monitor screen saver manager stopped.");
    }
}
