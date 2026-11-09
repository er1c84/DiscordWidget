using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using DiscordFloatingWidget.Services;
using DiscordFloatingWidget.Views;
using Microsoft.Win32;
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
        var registryPath = @"Software\DiscordFloatingWidget.Tests\" + Guid.NewGuid().ToString("N");
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
                    Directory.CreateDirectory(directory);
                    var executable = Path.Combine(directory, "Test widget.exe");
                    File.WriteAllText(executable, "Test fixture; never executed.");
                    var startup = new StartupService(executable, registryPath);
                    using var tray = new TrayIconService(window, startup);
                    window.Exiting += tray.Dispose;
                    Assert.True(window.CloseToTray);
                    Assert.Equal("Hide to system tray", ((Button)window.FindName("CloseButton")).ToolTip);
                    Assert.False(tray.StartupItem.Checked);

                    window.Close(); // X/Alt+F4 hides without starting shutdown.
                    await Idle();
                    Assert.False(window.IsVisible);
                    Assert.False(window.IsExiting);
                    tray.RefreshMenu();
                    Assert.Equal("Show Widget", tray.VisibilityItem.Text);
                    tray.VisibilityItem.PerformClick();
                    await Idle();
                    Assert.True(window.IsVisible);
                    window.ShowAndFocus(); // Repeated activation must never toggle it off.
                    Assert.True(window.IsVisible);
                    tray.RefreshMenu();
                    Assert.Equal("Hide Widget", tray.VisibilityItem.Text);
                    tray.VisibilityItem.PerformClick();
                    await Idle();
                    Assert.False(window.IsVisible);
                    window.ShowAndFocus();
                    await Idle();

                    tray.StartupItem.PerformClick();
                    await Idle();
                    Assert.Equal((true, null), startup.Read());
                    Assert.True(tray.StartupItem.Checked);
                    tray.StartupItem.PerformClick();
                    await Idle();
                    Assert.Equal((false, null), startup.Read());
                    Assert.False(tray.StartupItem.Checked);
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
                    tray.RefreshMenu();
                    Assert.Equal("Show Widget", tray.VisibilityItem.Text);
                    tray.VisibilityItem.PerformClick();
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
                    window.HideToTray();
                    var exited = new TaskCompletionSource();
                    window.Closed += (_, _) => exited.SetResult();
                    tray.ExitItem.PerformClick();
                    await exited.Task;
                    Assert.True(window.IsExiting);
                    Assert.False(window.CloseToTray);
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
                    restored.CloseToTray = true;
                    restored.PrepareForSessionEnd();
                    await CloseAsync(restored);
                    Assert.NotNull((await settings.LoadAsync()).Layout);
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
            Registry.CurrentUser.DeleteSubKeyTree(registryPath, throwOnMissingSubKey: false);
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
