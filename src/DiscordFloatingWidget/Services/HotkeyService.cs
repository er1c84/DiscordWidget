using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DiscordFloatingWidget.Services;

// A single registration belongs to the existing HWND and its UI thread.
internal sealed class HotkeyService : IDisposable
{
    internal const int HotkeyId = 0x4457;
    internal const int HotkeyMessage = 0x0312;
    private const uint NoRepeat = 0x4000;
    private readonly HwndSource _source;
    private readonly Action _onPressed;
    private readonly uint _modifiers;
    private readonly uint _virtualKey;
    private bool _disposed;

    public bool IsRegistered { get; private set; }

    public HotkeyService(HwndSource source, Action onPressed, uint modifiers = 0x0006, uint virtualKey = 0x44)
    {
        _source = source;
        _onPressed = onPressed;
        _modifiers = modifiers; // MOD_CONTROL | MOD_SHIFT by default
        _virtualKey = virtualKey; // D by default
        source.Dispatcher.VerifyAccess();
        source.Disposed += SourceDisposed;
        source.AddHook(WindowMessage);
    }

    public string? Register()
    {
        _source.Dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed || _source.IsDisposed, this);
        if (IsRegistered) return null;
        if (RegisterHotKey(_source.Handle, HotkeyId, _modifiers | NoRepeat, _virtualKey))
        {
            IsRegistered = true;
            return null;
        }

        var error = Marshal.GetLastWin32Error();
        return "The global shortcut could not be registered. Another application may already be using it. " +
            "The widget will continue without a shortcut. Minimize and close still work normally. " +
            "To try again, close the other application using the shortcut and restart the widget.\n\n" +
            $"Windows reported: {new Win32Exception(error).Message} (error {error}).";
    }

    private nint WindowMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        var keys = unchecked((uint)(long)lParam);
        if (!_disposed && IsRegistered && message == HotkeyMessage && wParam == HotkeyId
            && (keys & 0xFFFF) == _modifiers && (keys >> 16) == _virtualKey)
        {
            handled = true;
            // Never change window visibility from inside the native message callback.
            _source.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (!_disposed && IsRegistered) _onPressed();
            }));
        }
        return 0;
    }

    public void Dispose()
    {
        _source.Dispatcher.VerifyAccess();
        if (_disposed) return;
        _disposed = true;
        if (IsRegistered && !_source.IsDisposed && !UnregisterHotKey(_source.Handle, HotkeyId))
        {
            // Window destruction also releases the registration; retain diagnostics if
            // explicit cleanup fails without interrupting application shutdown.
            Trace.TraceWarning($"Global shortcut cleanup failed: Windows error {Marshal.GetLastWin32Error()}.");
        }
        IsRegistered = false;
        _source.Disposed -= SourceDisposed;
        if (!_source.IsDisposed) _source.RemoveHook(WindowMessage);
    }

    private void SourceDisposed(object? sender, EventArgs e) => Dispose();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint window, int id);
}
