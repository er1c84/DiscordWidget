using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using DiscordFloatingWidget.Services;
using Xunit;

namespace DiscordFloatingWidget.Tests;

public sealed class HotkeyServiceTests
{
    [Fact]
    public async Task NativeRegistrationRoutesMessagesReportsConflictsAndReleasesShortcut()
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    using var firstSource = CreateSource("First hotkey test");
                    using var secondSource = CreateSource("Second hotkey test");
                    var presses = 0;
                    // Never reserve the user's actual Ctrl+Shift+D shortcut during tests.
                    // Find an available Ctrl+Alt+Shift+F13..F24 combination instead.
                    const uint modifiers = 0x0007;
                    uint key = 0x87;
                    HotkeyService? first = null;
                    for (; key >= 0x7C; key--)
                    {
                        var candidate = new HotkeyService(firstSource, () => presses++, modifiers, key);
                        if (candidate.Register() is null) { first = candidate; break; }
                        candidate.Dispose();
                    }
                    Assert.NotNull(first);
                    using (first)
                    {
                        Assert.True(first.IsRegistered);
                        Assert.Null(first.Register()); // Idempotent, no duplicate OS registration.
                        using var second = new HotkeyService(secondSource, () => { }, modifiers, key);
                        Assert.NotNull(second.Register());
                        Assert.False(second.IsRegistered);

                        Post(firstSource, HotkeyService.HotkeyId + 1, modifiers, key);
                        Post(firstSource, HotkeyService.HotkeyId, modifiers, key - 1);
                        await Idle();
                        Assert.Equal(0, presses);

                        Post(firstSource, HotkeyService.HotkeyId, modifiers, key);
                        await Idle();
                        Assert.Equal(1, presses);

                        first.Dispose();
                        first.Dispose();
                        Assert.False(first.IsRegistered);
                        Assert.Throws<ObjectDisposedException>(() => first.Register());
                        Assert.Null(second.Register()); // The exact combination was released.
                        Post(firstSource, HotkeyService.HotkeyId, modifiers, key);
                        await Idle();
                        Assert.Equal(1, presses);
                    }

                    using var destroyedSource = CreateSource("Disposed source test");
                    using var destroyed = new HotkeyService(destroyedSource, () => presses++, modifiers, key);
                    Assert.Null(destroyed.Register());
                    destroyedSource.Dispose();
                    Assert.False(destroyed.IsRegistered);
                    using var afterDestruction = new HotkeyService(firstSource, () => { }, modifiers, key);
                    Assert.Null(afterDestruction.Register());
                    completed.SetResult();
                }
                catch (Exception ex) { completed.SetException(ex); }
                finally { dispatcher.InvokeShutdown(); }
            }));
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
    }

    private static HwndSource CreateSource(string name) => new(new HwndSourceParameters(name)
    {
        ParentWindow = new nint(-3), // HWND_MESSAGE: no visible test window.
        WindowStyle = 0
    });

    private static async Task Idle() => await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

    private static void Post(HwndSource source, int id, uint modifiers, uint key)
    {
        if (!PostMessage(source.Handle, HotkeyService.HotkeyMessage, id, (nint)((key << 16) | modifiers)))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(nint hwnd, int message, nint wParam, nint lParam);
}
