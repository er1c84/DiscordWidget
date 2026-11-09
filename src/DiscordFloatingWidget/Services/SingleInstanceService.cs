using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace DiscordFloatingWidget.Services;

// The UI thread owns the mutex until exit. A current-user-only pipe transfers
// activation requests, including requests arriving while startup is still loading.
internal sealed class SingleInstanceService : IDisposable
{
    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private readonly CancellationTokenSource _stop = new();
    private Task? _listener;
    private bool _disposed;

    internal bool IsPrimary { get; }

    internal SingleInstanceService(string? instanceName = null)
    {
        _pipeName = instanceName ?? $"DiscordFloatingWidget.{WindowsIdentity.GetCurrent().User!.Value}.{Process.GetCurrentProcess().SessionId}";
        _mutex = new Mutex(initiallyOwned: false, @"Local\" + _pipeName + ".Instance");
        try
        {
            try { IsPrimary = _mutex.WaitOne(0); }
            catch (AbandonedMutexException) { IsPrimary = true; }
        }
        catch
        {
            _mutex.Dispose();
            _stop.Dispose();
            throw;
        }
    }

    internal void StartListening(Action activate, Action<string> onError)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsPrimary || _listener is not null) throw new InvalidOperationException("Only the primary instance can listen once.");
        _listener = ListenAsync(activate, onError, _stop.Token);
    }

    internal async Task<bool> NotifyPrimaryAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsPrimary) throw new InvalidOperationException("This is already the primary instance.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            using var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
            using var reader = new StreamReader(pipe, leaveOpen: true);
            using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
            var identity = await reader.ReadLineAsync(timeout.Token).ConfigureAwait(false);
            if (!int.TryParse(identity, NumberStyles.None, CultureInfo.InvariantCulture, out var processId) || processId <= 0)
                return false;
            // The freshly launched process can grant the running widget foreground
            // activation permission. Windows may still restrict focus in some apps.
            if (!AllowSetForegroundWindow((uint)processId))
                Trace.TraceInformation("Windows did not grant foreground activation to the widget.");
            await writer.WriteLineAsync("SHOW".AsMemory(), timeout.Token).ConfigureAwait(false);
            return await reader.ReadLineAsync(timeout.Token).ConfigureAwait(false) == "OK";
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Widget activation could not be delivered: {ex.Message}");
            return false;
        }
    }

    private async Task ListenAsync(Action activate, Action<string> onError, CancellationToken stopping)
    {
        try
        {
            while (!stopping.IsCancellationRequested)
            {
                using var pipe = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(stopping).ConfigureAwait(false);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stopping);
                timeout.CancelAfter(TimeSpan.FromSeconds(2));
                try
                {
                    using var reader = new StreamReader(pipe, leaveOpen: true);
                    using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
                    await writer.WriteLineAsync(Environment.ProcessId.ToString(CultureInfo.InvariantCulture).AsMemory(), timeout.Token)
                        .ConfigureAwait(false);
                    if (await reader.ReadLineAsync(timeout.Token).ConfigureAwait(false) == "SHOW")
                    {
                        activate();
                        await writer.WriteLineAsync("OK".AsMemory(), timeout.Token).ConfigureAwait(false);
                    }
                }
                catch (Exception ex) when (ex is IOException or OperationCanceledException)
                {
                    // A launcher can disappear or time out; keep accepting later requests.
                    Trace.TraceInformation($"Widget launcher disconnected: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!stopping.IsCancellationRequested)
                onError($"The running widget cannot receive requests from the desktop shortcut. " +
                    $"Use the tray icon or Ctrl + Shift + D to show it.\n\n{ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (IsPrimary && Environment.CurrentManagedThreadId != _ownerThread)
            throw new InvalidOperationException("The primary instance must be disposed on its owning UI thread.");
        _disposed = true;
        _stop.Cancel();
        // All listener awaits avoid the UI context, so cancellation can finish
        // before another process acquires the mutex and creates its own pipe.
        _listener?.GetAwaiter().GetResult();
        if (IsPrimary) _mutex.ReleaseMutex();
        _mutex.Dispose();
        _stop.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(uint processId);
}
