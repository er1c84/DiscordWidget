using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using DiscordFloatingWidget.Models;

namespace DiscordFloatingWidget.Services;

// Owns native display handling and layout persistence; the view keeps only presentation logic.
public sealed class WindowStateService
{
    private readonly Window _window;
    private readonly SettingsService _settings;
    private readonly DispatcherTimer _saveTimer;
    private WindowLayout? _lastNormalLayout;
    private HwndSource? _source;
    private nint _handle;
    private bool _applying, _stopped, _recoveryQueued, _inSizeMove;
    private bool _warned;

    public event Action<string>? Warning;

    public WindowStateService(Window window, SettingsService settings, WindowLayout? initialLayout)
    {
        _window = window;
        _settings = settings;
        _lastNormalLayout = initialLayout;
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _saveTimer.Tick += SaveTimer_Tick;
        window.SourceInitialized += SourceInitialized;
        window.LocationChanged += GeometryChanged;
        window.SizeChanged += GeometryChanged;
        window.StateChanged += StateChanged;
    }

    private void SourceInitialized(object? sender, EventArgs e)
    {
        _handle = new WindowInteropHelper(_window).Handle;
        _source = HwndSource.FromHwnd(_handle);
        _source?.AddHook(WindowMessage);
        TryDisplayAction(() =>
        {
            var area = WindowsDisplay.WorkArea(_lastNormalLayout is { } layout
                ? LayoutGeometry.SavedBounds(layout) : WindowsDisplay.GetBounds(_handle));
            _applying = true;
            try
            {
                // Move the still-hidden HWND onto the target monitor first. WPF processes
                // WM_DPICHANGED, then GetDpiForWindow gives that monitor's effective DPI.
                WindowsDisplay.Move(_handle, new PixelBounds(area.Left, area.Top, 1, 1));
                Apply(LayoutGeometry.Restore(_lastNormalLayout, area, WindowsDisplay.Scale(_handle)), area);
            }
            finally { _applying = false; }
        });
        Capture();
    }

    private void Apply(PixelBounds bounds, PixelBounds area)
    {
        var scale = WindowsDisplay.Scale(_handle);
        _window.MinWidth = Math.Min(LayoutGeometry.MinimumWidth, area.Width / scale);
        _window.MinHeight = Math.Min(LayoutGeometry.MinimumHeight, area.Height / scale);
        WindowsDisplay.Move(_handle, bounds);
    }

    public void Resize(double width, double height)
    {
        if (_window.WindowState != WindowState.Normal) _window.WindowState = WindowState.Normal;
        TryDisplayAction(() =>
        {
            var current = WindowsDisplay.GetBounds(_handle);
            var scale = WindowsDisplay.Scale(_handle);
            var area = WindowsDisplay.WorkArea(current);
            Apply(LayoutGeometry.Constrain(current with
            {
                Width = (int)Math.Round(width * scale), Height = (int)Math.Round(height * scale)
            }, area, scale), area);
        });
        Capture();
    }

    private void GeometryChanged(object? sender, EventArgs e)
    {
        if (_applying || _stopped || _handle == 0 || _window.WindowState != WindowState.Normal) return;
        Capture();
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void Capture()
    {
        if (_handle == 0 || _applying || _stopped || _window.WindowState != WindowState.Normal
            || !WindowsDisplay.IsNormal(_handle)) return;
        TryDisplayAction(() =>
        {
            var bounds = WindowsDisplay.GetBounds(_handle);
            var scale = WindowsDisplay.Scale(_handle);
            var layout = new WindowLayout(bounds.Left, bounds.Top, bounds.Width / scale, bounds.Height / scale, scale);
            if (layout.IsValid) _lastNormalLayout = layout;
        });
    }

    private void StateChanged(object? sender, EventArgs e)
    {
        if (_window.WindowState == WindowState.Normal) QueueRecovery();
    }

    public void RecoverVisibleLayout() => QueueRecovery();

    private nint WindowMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        // Let WPF handle DPI scaling and native caption/resize messages first.
        // Recovery never runs inside a drag, so moving between monitors stays unrestricted.
        if (message == 0x0231) _inSizeMove = true; // ENTERSIZEMOVE
        if (message == 0x0232) _inSizeMove = false;
        if (message is 0x007E or 0x02E0 or 0x0232 // DISPLAYCHANGE, DPICHANGED, EXITSIZEMOVE
            || (message == 0x001A && wParam == 0x002F)) // SETTINGCHANGE / SPI_SETWORKAREA
            QueueRecovery();
        return 0;
    }

    private void QueueRecovery()
    {
        if (_recoveryQueued || _stopped || _applying) return;
        _recoveryQueued = true;
        _window.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            _recoveryQueued = false;
            if (_stopped || _inSizeMove || _window.WindowState != WindowState.Normal) return;
            TryDisplayAction(() =>
            {
                var bounds = WindowsDisplay.GetBounds(_handle);
                var area = WindowsDisplay.WorkArea(bounds);
                _applying = true;
                try { Apply(LayoutGeometry.Constrain(bounds, area, WindowsDisplay.Scale(_handle)), area); }
                finally { _applying = false; }
            });
            GeometryChanged(this, EventArgs.Empty);
        }));
    }

    private async void SaveTimer_Tick(object? sender, EventArgs e)
    {
        _saveTimer.Stop();
        if (_lastNormalLayout is { } layout)
            Report(await _settings.SaveAsync(layout));
    }

    public async Task StopAndSaveAsync()
    {
        Stop();
        if (_lastNormalLayout is { } layout) Report(await _settings.SaveAsync(layout));
    }

    // Windows session ending is synchronous; complete the small serialized write
    // without depending on a dispatcher that Windows is about to shut down.
    internal string? StopAndSaveForSessionEnd()
    {
        Stop();
        return _lastNormalLayout is { } layout ? _settings.SaveAsync(layout).GetAwaiter().GetResult() : null;
    }

    private void Stop()
    {
        Capture();
        _stopped = true;
        _saveTimer.Stop();
        _saveTimer.Tick -= SaveTimer_Tick;
        _source?.RemoveHook(WindowMessage);
        _window.SourceInitialized -= SourceInitialized;
        _window.LocationChanged -= GeometryChanged;
        _window.SizeChanged -= GeometryChanged;
        _window.StateChanged -= StateChanged;
    }

    private void TryDisplayAction(Action action)
    {
        try { action(); }
        catch (Win32Exception ex) { Report($"Windows could not update the widget placement.\n\n{ex.Message}"); }
    }

    private void Report(string? warning)
    {
        if (warning is null || _warned) return;
        _warned = true;
        Warning?.Invoke(warning);
    }
}
