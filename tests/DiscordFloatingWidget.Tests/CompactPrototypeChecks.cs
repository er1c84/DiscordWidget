using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DiscordFloatingWidget.Models;
using DiscordFloatingWidget.Services;
using DiscordFloatingWidget.Views;
using Microsoft.Web.WebView2.Core;
using Xunit;

namespace DiscordFloatingWidget.Tests;

// Runs on the existing STA Application from WindowLifecycleTests. The fixtures
// are served by a native request interceptor into a new temporary browser profile;
// no real Discord account, private page, or message is accessed.
internal static class CompactPrototypeChecks
{
    internal static async Task RunAsync(string directory)
    {
        await IdleTimerCheckAsync();
        var favorites = new FavoritesService(Path.Combine(directory, "favorites.json"));
        var profile = Path.Combine(directory, "WebView2");
        var settings = new SettingsService(Path.Combine(directory, "settings.json"));
        var maya = FavoritesTests.Create("Maya", ConversationType.DirectMessage, "https://discord.com/channels/@me/123");
        var alex = FavoritesTests.Create("Alex", ConversationType.DirectMessage, "https://discord.com/channels/@me/456");
        var group = FavoritesTests.Create("Weekend crew", ConversationType.GroupDirectMessage, "https://discord.com/channels/@me/789");
        var legacy = FavoritesTests.Create("Saved channel", ConversationType.ServerChannel, "https://discord.com/channels/123/456");
        Assert.Null(await favorites.SaveAsync([maya, alex, group, legacy]));
        var window = new MainWindow(settings, null, null, favorites, profile) { ShowActivated = false };
        window.ContentView.Browser.Initialized += AttachFixture;
        window.Show();
        window.IsEnabled = false; // Keep human keystrokes out of the isolated fixture windows.
        try
        {
            await window.ContentView.LoadFavoritesAsync();
            var tabs = (TabControl)window.ContentView.FindName("NavigationTabs");
            var discordTab = (TabItem)window.ContentView.FindName("DiscordTab");
            var favoritesTab = (TabItem)window.ContentView.FindName("FavoritesTab");
            Assert.Same(favoritesTab, tabs.SelectedItem);
            Assert.Equal(4, window.ContentView.Favorites.Count);
            await CaptureOptionsAsync(window);
            await window.ContentView.OpenFavoriteAsync(maya);
            var core = window.ContentView.Browser.Core!;
            await WaitScript(core, "document.documentElement.hasAttribute('data-dw-focus')");
            Assert.Equal("Maya", ((TextBlock)window.FindName("HeaderTitle")).Text);
            await CheckComposer(core);
            CheckTabsVisible(window);
            await CaptureAsync(window, core, "compact-prototype");
            window.Width = 260; window.Height = 300;
            await Task.Delay(250);
            await CheckComposer(core);
            CheckTabsVisible(window);
            Assert.Equal(Visibility.Visible, ((ContentControl)window.ContentView.FindName("BrowserHost")).Visibility);
            Assert.Equal("none", JsonSerializer.Deserialize<string>(await core.ExecuteScriptAsync("getComputedStyle(document.querySelector('aside')).display")));
            await core.ExecuteScriptAsync("document.querySelector('[role=textbox]').focus(); document.execCommand('insertText', false, 'Unsent fixture draft');");
            var navigations = 0;
            void CountNavigation(object? sender, CoreWebView2NavigationStartingEventArgs args) => navigations++;
            core.NavigationStarting += CountNavigation;
            tabs.SelectedItem = favoritesTab;
            await Task.Delay(100);
            Assert.Equal(Visibility.Visible, ((Grid)window.ContentView.FindName("PickerPanel")).Visibility);
            CheckTabsVisible(window);
            await CaptureAsync(window, core, "compact-tabs-favorites");
            tabs.SelectedItem = discordTab;
            await Task.Delay(100);
            Assert.Same(core, window.ContentView.Browser.Core);
            Assert.Equal(Visibility.Visible, ((ContentControl)window.ContentView.FindName("BrowserHost")).Visibility);
            Assert.Equal(0, navigations);
            core.NavigationStarting -= CountNavigation;
            Assert.Equal("Unsent fixture draft", JsonSerializer.Deserialize<string>(await core.ExecuteScriptAsync("document.querySelector('[role=textbox]').textContent")));
            await window.ContentView.Browser.SetFocusEnabledAsync(false);
            await WaitScript(core, "!document.documentElement.hasAttribute('data-dw-focus')");
            Assert.NotEqual("none", JsonSerializer.Deserialize<string>(await core.ExecuteScriptAsync("getComputedStyle(document.querySelector('aside')).display")));
            Assert.Equal("Unsent fixture draft", JsonSerializer.Deserialize<string>(await core.ExecuteScriptAsync("document.querySelector('[role=textbox]').textContent")));
            await window.ContentView.Browser.SetFocusEnabledAsync(true);
            await WaitScript(core, "document.documentElement.hasAttribute('data-dw-focus')");
            await CaptureAsync(window, core, "compact-prototype-small");
            // Overlay containers remain reachable; losing the composer restores normal layout.
            await core.ExecuteScriptAsync("document.getElementById('overlays').innerHTML = '<div role=dialog>Fixture dialog</div>';");
            await Task.Delay(250);
            Assert.True(JsonSerializer.Deserialize<bool>(await core.ExecuteScriptAsync("getComputedStyle(document.getElementById('overlays')).display !== 'none'")));
            await core.ExecuteScriptAsync("document.querySelector('form').remove();");
            await WaitScript(core, "!document.documentElement.hasAttribute('data-dw-focus')");
            await window.ContentView.OpenFavoriteAsync(group);
            await WaitScript(core, "document.documentElement.hasAttribute('data-dw-focus') && location.pathname.endsWith('/789')");
            Assert.Equal(group.Id, window.ContentView.LastFavoriteId);
            await window.ContentView.Browser.NavigateAsync(new Uri("https://discord.com/login"));
            await WaitScript(core, "location.pathname === '/login' && !document.documentElement.hasAttribute('data-dw-focus')");
            await window.ContentView.OpenFavoriteAsync(group);
            await WaitScript(core, "document.documentElement.hasAttribute('data-dw-focus')");
        }
        finally { await CloseAsync(window); }

        // Restore through the real UI loading path, with all requests still intercepted.
        var restored = new MainWindow(settings, null, null, new FavoritesService(Path.Combine(directory, "favorites.json")), profile) { ShowActivated = false };
        restored.ContentView.Browser.Initialized += AttachFixture;
        restored.Show();
        restored.IsEnabled = false;
        try
        {
            await restored.ContentView.LoadFavoritesAsync();
            await WaitScript(restored.ContentView.Browser.Core!, "location.pathname.endsWith('/789') && document.documentElement.hasAttribute('data-dw-focus')");
            Assert.Equal("Weekend crew", ((TextBlock)restored.FindName("HeaderTitle")).Text);
            Assert.Equal(group.Id, restored.ContentView.LastFavoriteId);
        }
        finally { await CloseAsync(restored); }
        await BrowserSleepCheckAsync(Path.Combine(directory, "SleepProfile"));
    }

    private static void AttachFixture(CoreWebView2 core)
    {
        core.AddWebResourceRequestedFilter("https://discord.com/*", CoreWebView2WebResourceContext.All, CoreWebView2WebResourceRequestSourceKinds.All);
        core.WebResourceRequested += (_, args) =>
        {
            using var resource = typeof(CompactPrototypeChecks).Assembly.GetManifestResourceStream("DiscordFloatingWidget.Tests.Fixtures.discord-focus.html")!;
            using var reader = new StreamReader(resource);
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(reader.ReadToEnd()));
            args.Response = core.Environment.CreateWebResourceResponse(stream, 200, "OK", "Content-Type: text/html; charset=utf-8\r\nCache-Control: no-store");
        };
    }

    private static async Task CheckComposer(CoreWebView2 core)
    {
        await WaitScript(core, "(() => { const r = document.querySelector('[role=textbox]').getBoundingClientRect(); return r.width >= 100 && r.top >= 0 && r.bottom <= innerHeight + 1 && document.documentElement.scrollWidth <= innerWidth; })()");
    }

    private static void CheckTabsVisible(MainWindow window)
    {
        window.UpdateLayout();
        var toolbar = (Border)window.ContentView.FindName("Toolbar");
        foreach (var name in new[] { "DiscordTab", "FavoritesTab", "QuickAddButton", "MoreButton" })
        {
            var control = (FrameworkElement)window.ContentView.FindName(name);
            var bounds = control.TransformToAncestor(toolbar).TransformBounds(new Rect(control.RenderSize));
            Assert.True(control.IsVisible && bounds.Width > 0 && bounds.Height > 0, name + " must stay visible.");
            Assert.True(bounds.Left >= 0 && bounds.Right <= toolbar.ActualWidth
                && bounds.Top >= 0 && bounds.Bottom <= toolbar.ActualHeight, name + " must fit in the compact toolbar.");
        }
    }

    private static async Task IdleTimerCheckAsync()
    {
        Assert.Equal(TimeSpan.FromSeconds(60), BrowserIdleController.DefaultDelay);
        int suspends = 0, resumes = 0;
        var pending = new TaskCompletionSource<bool>();
        using var controller = new BrowserIdleController(() => { suspends++; return pending.Task; }, () => resumes++, TimeSpan.FromMilliseconds(30));
        controller.SetVisible(false);
        controller.SetVisible(true);
        await Task.Delay(80);
        Assert.Equal(0, suspends);
        controller.SetVisible(false);
        await Task.Delay(80);
        Assert.Equal(1, suspends);
        controller.SetVisible(true);
        pending.SetResult(true);
        await Task.Delay(30);
        Assert.Equal(3, resumes); // Cancel, restore, then late suspend completion.
        Assert.False(controller.TimerRunning);
        controller.SetVisible(false);
        controller.Dispose();
        await Task.Delay(80);
        Assert.Equal(1, suspends);
    }

    private static async Task BrowserSleepCheckAsync(string profile)
    {
        var host = new ContentControl();
        var window = new Window { Width = 320, Height = 420, Content = host, ShowActivated = false };
        var browser = new DiscordWebService(host, profile, TimeSpan.FromMilliseconds(100));
        browser.Initialized += AttachFixture;
        window.Show();
        window.IsEnabled = false;
        try
        {
            await browser.EnsureAsync();
            // Before navigation the top-level page is about:blank, outside the allowed origin.
            Assert.Equal("null", await browser.Core!.ExecuteScriptAsync(DiscordFocusScript.Source));
            Assert.True(JsonSerializer.Deserialize<bool>(await browser.Core.ExecuteScriptAsync("!window.__discordWidgetFocus")));
            await browser.NavigateAsync(new Uri("https://discord.com/channels/@me/123"));
            await WaitScript(browser.Core!, "document.documentElement.hasAttribute('data-dw-focus')");
            await browser.Core!.ExecuteScriptAsync("document.querySelector('[role=textbox]').textContent = 'Unsent sleep test';");
            Assert.Equal("Unsent sleep test", JsonSerializer.Deserialize<string>(await browser.Core.ExecuteScriptAsync("document.querySelector('[role=textbox]').textContent")));
            var perfPath = Environment.GetEnvironmentVariable("DISCORD_WIDGET_PERFORMANCE");
            if (!string.IsNullOrWhiteSpace(perfPath)) await Task.Delay(5000); // Exclude browser startup from the sample.
            var active = string.IsNullOrWhiteSpace(perfPath) ? null : await MeasureAsync(browser.Core.Environment, "visible fixture");
            window.Hide(); browser.SetWindowVisible(false);
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!browser.IsSleeping && DateTime.UtcNow < deadline) await Task.Delay(50);
            Assert.True(browser.IsSleeping);
            if (active is not null)
            {
                await Task.Delay(1000);
                var sleeping = await MeasureAsync(browser.Core.Environment, "suspended fixture");
                Directory.CreateDirectory(Path.GetDirectoryName(perfPath!)!);
                await File.WriteAllTextAsync(perfPath!, "Isolated synthetic conversation; includes test host and its WebView2 processes. Not a signed-in Discord benchmark.\n" + active + "\n" + sleeping);
            }
            browser.SetWindowVisible(true); window.Show();
            await Task.Delay(100);
            Assert.False(browser.IsSleeping);
            Assert.Equal("Unsent sleep test", JsonSerializer.Deserialize<string>(await browser.Core!.ExecuteScriptAsync("document.querySelector('[role=textbox]').textContent")));
        }
        finally
        {
            var environment = browser.Core?.Environment;
            var exited = new TaskCompletionSource();
            if (environment is not null) environment.BrowserProcessExited += (_, _) => exited.TrySetResult();
            browser.Dispose(); window.Close();
            if (environment is not null) await exited.Task.WaitAsync(TimeSpan.FromSeconds(15));
        }
    }

    private static async Task<string> MeasureAsync(CoreWebView2Environment environment, string phase)
    {
        var ids = environment.GetProcessInfos().Select(info => info.ProcessId).Append(Environment.ProcessId).Distinct().ToArray();
        static (double Cpu, long Working, long Private) Read(int[] processIds)
        {
            double cpu = 0; long working = 0, privateBytes = 0;
            foreach (var id in processIds)
            {
                try { using var process = Process.GetProcessById(id); cpu += process.TotalProcessorTime.TotalMilliseconds; working += process.WorkingSet64; privateBytes += process.PrivateMemorySize64; }
                catch (ArgumentException) { }
            }
            return (cpu, working, privateBytes);
        }
        var before = Read(ids); var clock = Stopwatch.StartNew();
        await Task.Delay(5000);
        var after = Read(ids);
        return $"{phase}: CPU {(after.Cpu - before.Cpu) / clock.Elapsed.TotalMilliseconds / Environment.ProcessorCount * 100:F2}% of machine; working set {after.Working / 1048576.0:F1} MiB; private bytes {after.Private / 1048576.0:F1} MiB; {clock.Elapsed.TotalSeconds:F1}s sample";
    }

    private static async Task CaptureAsync(MainWindow window, CoreWebView2 core, string name)
    {
        var path = Environment.GetEnvironmentVariable("DISCORD_WIDGET_SCREENSHOT");
        if (string.IsNullOrWhiteSpace(path)) return;
        window.UpdateLayout();
        var browserHost = (ContentControl)window.ContentView.FindName("BrowserHost");
        BitmapImage? page = null;
        if (browserHost.IsVisible)
        {
            using var stream = new MemoryStream();
            await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
            stream.Position = 0;
            page = new BitmapImage(); page.BeginInit(); page.CacheOption = BitmapCacheOption.OnLoad; page.StreamSource = stream; page.EndInit();
        }
        var offset = browserHost.TransformToAncestor(window).Transform(new Point(0, 0));
        var visual = new DrawingVisual();
        var native = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        native.Render(window);
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawImage(native, new Rect(0, 0, window.ActualWidth, window.ActualHeight));
            if (page is not null)
                drawing.DrawImage(page, new Rect(offset.X, offset.Y, browserHost.ActualWidth, browserHost.ActualHeight));
        }
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var output = File.Create(Path.Combine(Path.GetDirectoryName(path)!, name + ".png")); encoder.Save(output);
    }

    private static async Task CaptureOptionsAsync(MainWindow window)
    {
        var path = Environment.GetEnvironmentVariable("DISCORD_WIDGET_SCREENSHOT");
        if (string.IsNullOrWhiteSpace(path)) return;
        var button = (Button)window.ContentView.FindName("MoreButton");
        var menu = button.ContextMenu;
        menu.PlacementTarget = button;
        menu.IsOpen = true;
        try
        {
            await Task.Delay(100);
            ((MenuItem)menu.Items[0]).Focus();
            menu.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(menu.ActualWidth),
                (int)Math.Ceiling(menu.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(menu);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var output = File.Create(Path.Combine(Path.GetDirectoryName(path)!, "dark-options-menu.png"));
            encoder.Save(output);
        }
        finally { menu.IsOpen = false; }
    }

    private static async Task WaitScript(CoreWebView2 core, string expression)
    {
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (DateTime.UtcNow < deadline)
        {
            if (await core.ExecuteScriptAsync(expression) == "true") return;
            await Task.Delay(50);
        }
        Assert.Fail("Fixture condition timed out: " + expression);
    }

    private static async Task CloseAsync(MainWindow window)
    {
        var environment = window.ContentView.Browser.Core?.Environment;
        var exited = new TaskCompletionSource();
        var closed = new TaskCompletionSource();
        if (environment is not null) environment.BrowserProcessExited += (_, _) => exited.TrySetResult();
        window.Closed += (_, _) => closed.TrySetResult();
        window.IsEnabled = true;
        window.RequestExit();
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(15));
        if (environment is not null) await exited.Task.WaitAsync(TimeSpan.FromSeconds(15));
    }
}
