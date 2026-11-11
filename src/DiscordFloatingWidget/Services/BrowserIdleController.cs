using System.Windows.Threading;

namespace DiscordFloatingWidget.Services;

// One timer, only while hidden/minimized. Resume also handles a suspend request
// completing after the user has already restored the window.
internal sealed class BrowserIdleController : IDisposable
{
    internal static readonly TimeSpan DefaultDelay = TimeSpan.FromSeconds(60);
    private readonly DispatcherTimer _timer;
    private readonly Func<Task<bool>> _suspend;
    private readonly Action _resume;
    private bool _visible = true, _disposed, _suspending;
    internal bool TimerRunning => _timer.IsEnabled;

    internal BrowserIdleController(Func<Task<bool>> suspend, Action resume, TimeSpan? delay = null)
    {
        _suspend = suspend;
        _resume = resume;
        _timer = new DispatcherTimer { Interval = delay ?? DefaultDelay };
        _timer.Tick += TimerTick;
    }

    internal void SetVisible(bool visible)
    {
        if (_disposed || _visible == visible) return;
        _visible = visible;
        _timer.Stop();
        if (visible) _resume();
        else _timer.Start();
    }

    private async void TimerTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        if (_disposed || _visible || _suspending) return;
        _suspending = true;
        try { await _suspend(); }
        finally
        {
            _suspending = false;
            if (!_disposed && _visible) _resume();
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= TimerTick;
    }
}
