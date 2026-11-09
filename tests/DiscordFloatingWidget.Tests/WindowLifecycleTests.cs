using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using DiscordFloatingWidget.Services;
using DiscordFloatingWidget.Views;
using Xunit;

namespace DiscordFloatingWidget.Tests;

public sealed class WindowLifecycleTests
{
    [Fact]
    public async Task NativeWindowSupportsCompactControlsAndRestoresNormalLayout()
    {
        // WPF requires an STA dispatcher. Test our own windows without injecting input
        // into another application or changing the user's display configuration.
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var directory = Path.Combine(Path.GetTempPath(), "DiscordWidgetTests", Guid.NewGuid().ToString("N"));
        var thread = new Thread(() =>
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/DiscordFloatingWidget;component/Resources/Theme.xaml")
            };
            app.Startup += async (_, _) =>
            {
                try
                {
                    var settings = new SettingsService(Path.Combine(directory, "settings.json"));
                    var window = new MainWindow(settings, null, null) { ShowActivated = false };
                    window.Show();
                    await Idle();
                    Assert.True(window.Topmost);
                    Assert.Equal(360, window.ActualWidth, 0);
                    Assert.Equal(500, window.ActualHeight, 0);

                    window.Width = 180;
                    window.Height = 120;
                    await Idle();
                    Assert.Equal(180, window.ActualWidth, 0);
                    Assert.Equal(120, window.ActualHeight, 0);
                    Assert.Equal(Visibility.Visible, ((StackPanel)window.FindName("CompactPlaceholder")).Visibility);
                    Assert.Equal(Visibility.Collapsed, ((StackPanel)window.FindName("FullPlaceholder")).Visibility);
                    Assert.Equal("DW", ((TextBlock)window.FindName("HeaderTitle")).Text);
                    var expand = (Button)window.FindName("ExpandButton");
                    window.ShowActivated = true; // Match production for foreground/maximized restoration.
                    var originalHandle = new WindowInteropHelper(window).Handle;
                    var originalBounds = new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight);
                    FocusManager.SetFocusedElement(window, expand);
                    window.ToggleVisibility();
                    await Idle();
                    Assert.False(window.IsVisible);
                    window.ToggleVisibility();
                    await Idle();
                    Assert.True(window.IsVisible);
                    Assert.Equal(WindowState.Normal, window.WindowState);
                    Assert.Equal(originalHandle, new WindowInteropHelper(window).Handle);
                    Assert.Equal(originalBounds, new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight));
                    Assert.Same(expand, FocusManager.GetFocusedElement(window));

                    window.WindowState = WindowState.Minimized;
                    await Idle();
                    window.ToggleVisibility();
                    await Idle();
                    Assert.True(window.IsVisible);
                    Assert.Equal(WindowState.Normal, window.WindowState);
                    Assert.Equal(originalBounds, new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight));

                    window.IsEnabled = false;
                    window.ToggleVisibility();
                    Assert.True(window.IsVisible); // Disabled modal owner must never be hidden.
                    window.IsEnabled = true;

                    expand.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    await Idle();
                    Assert.True(window.ActualWidth > 180);
                    expand.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    await Idle();
                    Assert.Equal(180, window.ActualWidth, 0);
                    Assert.Equal(120, window.ActualHeight, 0);

                    window.WindowState = WindowState.Maximized;
                    await Idle();
                    window.ToggleVisibility();
                    Assert.False(window.IsVisible);
                    window.ToggleVisibility();
                    await Idle();
                    Assert.Equal(WindowState.Maximized, window.WindowState);
                    window.WindowState = WindowState.Normal;
                    await Idle();

                    window.Left += 24;
                    window.Top += 16;
                    await Idle();
                    window.WindowState = WindowState.Maximized;
                    await Idle();
                    window.WindowState = WindowState.Minimized;
                    await Idle();
                    await CloseAsync(window);
                    var (saved, warning) = await settings.LoadAsync();
                    Assert.Null(warning);
                    Assert.NotNull(saved);
                    Assert.Equal(180, saved.Width, 0);
                    Assert.Equal(120, saved.Height, 0);

                    var restored = new MainWindow(settings, saved, null) { ShowActivated = false };
                    restored.Show();
                    await Idle();
                    Assert.Equal(WindowState.Normal, restored.WindowState);
                    Assert.Equal(180, restored.ActualWidth, 0);
                    Assert.Equal(120, restored.ActualHeight, 0);
                    var origin = restored.PointToScreen(new Point(0, 0));
                    Assert.InRange(Math.Abs(origin.X - saved.Left), 0, 2);
                    Assert.InRange(Math.Abs(origin.Y - saved.Top), 0, 2);
                    restored.ToggleVisibility();
                    Assert.False(restored.IsVisible);
                    await CloseAsync(restored);
                    completed.SetResult();
                }
                catch (Exception ex) { completed.SetException(ex); }
                finally { app.Shutdown(); }
            };
            app.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        try { await completed.Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken); }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task Idle() => await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

    private static Task CloseAsync(Window window)
    {
        var closed = new TaskCompletionSource();
        window.Closed += (_, _) => closed.SetResult();
        window.Close();
        return closed.Task;
    }
}
