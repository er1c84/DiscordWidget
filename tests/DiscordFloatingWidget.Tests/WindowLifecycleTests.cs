using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DiscordFloatingWidget.Services;
using DiscordFloatingWidget.Models;
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
                    var favorites = new FavoritesService(Path.Combine(directory, "favorites.json"));
                    var browserProfile = Path.Combine(directory, "WebView2");
                    var window = new MainWindow(settings, null, null, favorites, browserProfile) { ShowActivated = false };
                    window.Show();
                    await Idle();
                    await window.ContentView.LoadFavoritesAsync();
                    Assert.True(((Button)window.ContentView.FindName("AddButton")).IsEnabled);
                    Assert.Equal(Visibility.Visible, ((Grid)window.ContentView.FindName("PickerPanel")).Visibility);
                    var friend = FavoritesTests.Create("Test friend", ConversationType.DirectMessage, "https://discord.com/channels/@me/123");
                    Assert.Null(await window.ContentView.SaveFavoritesAsync([friend]));
                    Assert.Equal(friend, Assert.Single(window.ContentView.Favorites));
                    await Idle();
                    CaptureIfRequested(window);
                    if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISCORD_WIDGET_SCREENSHOT")))
                    {
                        var editor = new FavoriteDialog(window, friend) { ShowActivated = false };
                        editor.Show();
                        await Idle();
                        CaptureIfRequested(editor, "editor");
                        editor.Close();
                    }
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
                    Assert.True(window.ContentView.IsCompact);
                    Assert.Equal(Visibility.Visible, ((StackPanel)window.ContentView.FindName("CompactPanel")).Visibility);
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

                    var restored = new MainWindow(settings, saved, null, new FavoritesService(Path.Combine(directory, "favorites.json")), browserProfile)
                        { ShowActivated = false };
                    restored.Show();
                    await Idle();
                    await restored.ContentView.LoadFavoritesAsync();
                    Assert.Equal(friend, Assert.Single(restored.ContentView.Favorites));
                    await BrowserProfileCheckAsync(Path.Combine(directory, "BrowserCheck"));
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
        try { await completed.Task.WaitAsync(TimeSpan.FromSeconds(90), TestContext.Current.CancellationToken); }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            Registry.CurrentUser.DeleteSubKeyTree(registryPath, throwOnMissingSubKey: false);
        }
    }

    private static async Task Idle() => await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

    private static async Task BrowserProfileCheckAsync(string profile)
    {
        var host = new ContentControl();
        var window = new Window { Content = host, Width = 600, Height = 400, ShowActivated = false, Title = "Widget browser check" };
        window.Show();
        var browser = new DiscordWebService(host, profile);
        try
        {
            Assert.True(await browser.EnsureAsync(), browser.Status);
            Assert.False(browser.Core!.Settings.AreHostObjectsAllowed);
            Assert.False(browser.Core.Settings.IsWebMessageEnabled);
            Assert.False(browser.Core.Settings.IsPasswordAutosaveEnabled);
            // A synthetic cookie in a unique test profile verifies the native clear
            // API. Never inspect the real user's profile or Discord authentication.
            var cookie = browser.Core.CookieManager.CreateCookie("widget_test", "fixture", "example.test", "/");
            cookie.Expires = DateTime.UtcNow.AddDays(1);
            browser.Core.CookieManager.AddOrUpdateCookie(cookie);
            Assert.Single(await browser.Core.CookieManager.GetCookiesAsync("https://example.test/"));
            await DisposeBrowserAsync(browser);
            browser = new DiscordWebService(host, profile);
            Assert.True(await browser.EnsureAsync(), browser.Status);
            Assert.Single(await browser.Core!.CookieManager.GetCookiesAsync("https://example.test/"));
            Assert.True(await browser.ClearDataAsync(), browser.Status);
            Assert.Equal(DiscordUrlPolicy.Home, browser.RequestedDestination);
            Assert.True(await browser.EnsureAsync(), browser.Status);
            Assert.Empty(await browser.Core!.CookieManager.GetCookiesAsync("https://example.test/"));
            var smokePath = Environment.GetEnvironmentVariable("DISCORD_WIDGET_NETWORK_SMOKE");
            if (!string.IsNullOrWhiteSpace(smokePath))
            {
                // Explicit opt-in integration check: load the public Discord page
                // in the temporary, unauthenticated profile. No login automation.
                var loaded = new TaskCompletionSource<Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                browser.Core.NavigationCompleted += (_, args) => loaded.TrySetResult(args);
                await browser.NavigateAsync(DiscordUrlPolicy.Home);
                var navigation = await loaded.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
                Directory.CreateDirectory(Path.GetDirectoryName(smokePath)!);
                await File.WriteAllTextAsync(smokePath,
                    $"Public Discord page load: success={navigation.IsSuccess}; HTTP={navigation.HttpStatusCode}; error={navigation.WebErrorStatus}",
                    TestContext.Current.CancellationToken);
                Assert.True(navigation.IsSuccess, browser.Status);
            }
        }
        finally
        {
            await DisposeBrowserAsync(browser);
            window.Close();
        }
    }

    private static async Task DisposeBrowserAsync(DiscordWebService browser)
    {
        var environment = browser.Core?.Environment;
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (environment is not null) environment.BrowserProcessExited += (_, _) => exited.TrySetResult();
        browser.Dispose();
        if (environment is not null)
            await exited.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
    }

    private static void CaptureIfRequested(Window window, string? suffix = null)
    {
        var path = Environment.GetEnvironmentVariable("DISCORD_WIDGET_SCREENSHOT");
        if (string.IsNullOrWhiteSpace(path)) return;
        if (suffix is not null) path = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "-" + suffix + ".png");
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var output = File.Create(path);
        encoder.Save(output);
    }

    private static Task CloseAsync(Window window)
    {
        var closed = new TaskCompletionSource();
        window.Closed += (_, _) => closed.SetResult();
        window.Close();
        return closed.Task;
    }
}
