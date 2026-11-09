using System.IO;
using System.IO.Pipes;
using System.Windows.Threading;
using DiscordFloatingWidget.Services;
using Xunit;

namespace DiscordFloatingWidget.Tests;

public sealed class SingleInstanceServiceTests
{
    [Fact]
    public Task SecondaryLaunchesRequestActivationAndExitReleasesOwnership() => OnDispatcher(async () =>
    {
        var name = "DiscordWidgetTests." + Guid.NewGuid().ToString("N");
        var activations = 0;
        using var primary = new SingleInstanceService(name);
        Assert.True(primary.IsPrimary);
        primary.StartListening(() => Interlocked.Increment(ref activations), FailListener);
        for (var i = 0; i < 3; i++)
            Assert.True(await NotifyFromAnotherThread(name));
        Assert.Equal(3, activations);
        primary.Dispose();
        primary.Dispose();

        using var replacement = new SingleInstanceService(name);
        Assert.True(replacement.IsPrimary);
        replacement.StartListening(() => Interlocked.Increment(ref activations), FailListener);
        Assert.True(await NotifyFromAnotherThread(name));
        Assert.Equal(4, activations);
    });

    [Fact]
    public Task LaunchDuringInitializationWaitsForThePrimaryListener() => OnDispatcher(async () =>
    {
        var name = "DiscordWidgetTests." + Guid.NewGuid().ToString("N");
        using var primary = new SingleInstanceService(name);
        var secondaryCreated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = NotifyFromAnotherThread(name, () => secondaryCreated.SetResult());
        await secondaryCreated.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        var activated = false;
        primary.StartListening(() => activated = true, FailListener);
        Assert.True(await request);
        Assert.True(activated);
    });

    [Fact]
    public Task DisconnectedOrInvalidClientDoesNotBreakFutureLaunches() => OnDispatcher(async () =>
    {
        var name = "DiscordWidgetTests." + Guid.NewGuid().ToString("N");
        using var primary = new SingleInstanceService(name);
        primary.StartListening(() => { }, FailListener);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        using (var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous))
        {
            await client.ConnectAsync(timeout.Token);
            using var reader = new StreamReader(client, leaveOpen: true);
            Assert.NotNull(await reader.ReadLineAsync(timeout.Token));
            using var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };
            await writer.WriteLineAsync("INVALID".AsMemory(), timeout.Token);
        }
        Assert.True(await NotifyFromAnotherThread(name));
    });

    [Fact]
    public void AbandonedOwnerCanBeReplacedAfterACrash()
    {
        var name = "DiscordWidgetTests." + Guid.NewGuid().ToString("N");
        using var abandoned = new Mutex(false, @"Local\" + name + ".Instance");
        var thread = new Thread(() => abandoned.WaitOne()) { IsBackground = true };
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(3)));
        using var recovered = new SingleInstanceService(name);
        Assert.True(recovered.IsPrimary);
    }

    private static Task<bool> NotifyFromAnotherThread(string name, Action? ready = null)
    {
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        // A mutex is recursive on its owning thread: use a distinct native thread
        // so this exercises the same ownership boundary as a second process.
        var thread = new Thread(async () =>
        {
            try
            {
                using var secondary = new SingleInstanceService(name);
                Assert.False(secondary.IsPrimary);
                ready?.Invoke();
                completed.SetResult(await secondary.NotifyPrimaryAsync());
            }
            catch (Exception ex) { completed.SetException(ex); }
        }) { IsBackground = true };
        thread.Start();
        return completed.Task.WaitAsync(TimeSpan.FromSeconds(8), TestContext.Current.CancellationToken);
    }

    private static Task OnDispatcher(Func<Task> scenario)
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            dispatcher.BeginInvoke(new Action(async () =>
            {
                try { await scenario(); completed.SetResult(); }
                catch (Exception ex) { completed.SetException(ex); }
                finally { dispatcher.InvokeShutdown(); }
            }));
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completed.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
    }

    private static void FailListener(string warning) => throw new InvalidOperationException(warning);
}
