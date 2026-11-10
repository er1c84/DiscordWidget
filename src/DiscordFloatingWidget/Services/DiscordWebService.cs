using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace DiscordFloatingWidget.Services;

// Owns one lazily initialized browser on the WPF dispatcher. It never reads
// Discord DOM, tokens, messages, cookies, or passwords, and exposes no native API
// to the page. Favorites contain only manually supplied conversation URLs.
internal sealed class DiscordWebService(ContentControl host, string profileDirectory) : IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private WebView2? _view;
    private Task<bool>? _initializing;
    private bool _disposed, _clearing, _recreate;
    private ulong _navigationId;
    private int _requestVersion;

    internal static string DefaultProfileDirectory => Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), "DiscordFloatingWidget", "WebView2");
    internal static Uri RuntimeDownload { get; } = new("https://developer.microsoft.com/microsoft-edge/webview2/#download-section");
    internal Uri RequestedDestination { get; private set; } = DiscordUrlPolicy.Home;
    internal string Status { get; private set; } = "Choose a favorite or open Discord.";
    internal bool HasError { get; private set; }
    internal bool IsBusy { get; private set; }
    internal bool RuntimeMissing { get; private set; }
    internal bool IsReady => !_disposed && !_recreate && _view?.CoreWebView2 is not null;
    internal bool CanGoBack => IsReady && _view!.CoreWebView2.CanGoBack;
    internal CoreWebView2? Core => IsReady ? _view!.CoreWebView2 : null;
    internal event Action? Changed;

    internal async Task NavigateAsync(Uri destination)
    {
        host.Dispatcher.VerifyAccess();
        if (_disposed || _clearing || !DiscordUrlPolicy.IsDiscord(destination)) return;
        RequestedDestination = DiscordUrlPolicy.TryConversation(destination.AbsoluteUri, out var conversation)
            ? conversation : DiscordUrlPolicy.Home;
        var request = ++_requestVersion;
        if (!await EnsureAsync() || _disposed || request != _requestVersion) return;
        try { _view!.CoreWebView2.Navigate(destination.AbsoluteUri); }
        catch (Exception ex) when (IsBrowserError(ex)) { Fail("Discord could not open. Try Reload or Open in Browser."); }
    }

    internal async Task<bool> EnsureAsync()
    {
        host.Dispatcher.VerifyAccess();
        if (_disposed) return false;
        if (_recreate) ResetBrowser();
        _initializing ??= InitializeAsync();
        var ready = await _initializing;
        if (!ready) _initializing = null; // A missing runtime or transient failure can be retried.
        return ready && !_disposed;
    }

    private async Task<bool> InitializeAsync()
    {
        SetStatus("Preparing Discord…", busy: true);
        try
        {
            // Throws a clear runtime-not-found exception before creating the profile.
            _ = CoreWebView2Environment.GetAvailableBrowserVersionString();
            Directory.CreateDirectory(profileDirectory);
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: profileDirectory);
            if (_disposed) return false;
            _view = new WebView2 { DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 30, 31, 34) };
            host.Content = _view;
            await _view.EnsureCoreWebView2Async(environment);
            if (_disposed) return false;
            var core = _view.CoreWebView2;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsWebMessageEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.NavigationStarting += NavigationStarting;
            core.NavigationCompleted += NavigationCompleted;
            core.HistoryChanged += HistoryChanged;
            core.SourceChanged += SourceChanged;
            core.NewWindowRequested += NewWindowRequested;
            core.PermissionRequested += PermissionRequested;
            core.ProcessFailed += ProcessFailed;
            core.DownloadStarting += DownloadStarting;
            RuntimeMissing = false;
            SetStatus("Discord is ready.");
            return true;
        }
        catch (WebView2RuntimeNotFoundException)
        {
            RuntimeMissing = true;
            ResetBrowser();
            Fail("Microsoft Edge WebView2 Runtime is missing. Install it using the Runtime link, then choose Reload. You can also open Discord in your browser.");
            return false;
        }
        catch (Exception ex) when (IsBrowserError(ex))
        {
            ResetBrowser();
            Fail("The embedded browser could not start. Choose Reload to retry, or Open in Browser. Check that your local profile folder is writable.");
            return false;
        }
    }

    internal void Back()
    {
        if (CanGoBack && !_clearing)
        {
            try { _view!.CoreWebView2.GoBack(); }
            catch (Exception ex) when (IsBrowserError(ex)) { Fail("Back is unavailable. Choose Reload or a favorite."); }
        }
    }

    internal Task ReloadAsync() => NavigateAsync(RequestedDestination);

    internal Uri ExternalDestination => DiscordUrlPolicy.BrowserDestination(
        IsReady && Uri.TryCreate(_view!.CoreWebView2.Source, UriKind.Absolute, out var uri) ? uri : null, RequestedDestination);

    internal async Task<bool> ClearDataAsync()
    {
        host.Dispatcher.VerifyAccess();
        if (_disposed || _clearing) return false;
        _clearing = true;
        ++_requestVersion;
        try
        {
            if (!await EnsureAsync()) return false;
            var core = _view!.CoreWebView2;
            SetStatus("Clearing the widget’s browser data…", busy: true);
            core.Stop();
            // Unload Discord before clearing data so its running scripts cannot
            // immediately write authenticated state back into the profile.
            var unloaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            ulong blankId = 0;
            void BlankStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
            {
                if (e.Uri == "about:blank") blankId = e.NavigationId;
            }
            void BlankCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
            {
                if (e.NavigationId == blankId && core.Source == "about:blank")
                {
                    if (e.IsSuccess) unloaded.TrySetResult();
                    else unloaded.TrySetException(new InvalidOperationException("The page could not be unloaded."));
                }
            }
            core.NavigationCompleted += BlankCompleted;
            core.NavigationStarting += BlankStarting;
            try
            {
                core.Navigate("about:blank");
                await unloaded.Task.WaitAsync(TimeSpan.FromSeconds(10), _lifetime.Token);
                await core.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.AllProfile);
            }
            finally
            {
                if (!_disposed)
                {
                    core.NavigationCompleted -= BlankCompleted;
                    core.NavigationStarting -= BlankStarting;
                }
            }
            if (_disposed) return false;
            ResetBrowser();
            RequestedDestination = DiscordUrlPolicy.Home;
            SetStatus("Browser data cleared. Favorites are unchanged. Open Discord to sign in again.");
            return true;
        }
        catch (Exception ex) when (IsBrowserError(ex) || ex is TimeoutException or OperationCanceledException)
        {
            Fail("Browser data could not be fully cleared. Choose Reload and try again. Your favorites are unchanged.");
            return false;
        }
        finally { _clearing = false; }
    }

    private void NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (_clearing && e.Uri == "about:blank") return;
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) || !DiscordUrlPolicy.IsDiscord(uri))
        {
            e.Cancel = true;
            if (uri is not null && DiscordUrlPolicy.IsExternalWeb(uri) && e.IsUserInitiated)
                QueueExternal(uri);
            else if (!_clearing)
                QueueStatus("That destination cannot open inside the widget. Use Open in Browser if Discord needs a different browser.");
            return;
        }
        if (_clearing) { e.Cancel = true; return; }
        _navigationId = e.NavigationId;
        SetStatus("Loading Discord…", busy: true);
    }

    private void NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (_clearing || e.NavigationId != _navigationId) return;
        if (e.IsSuccess) SetStatus("Discord Web · unofficial companion");
        else if (e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled)
            Fail($"Discord could not load ({e.WebErrorStatus}). Check your connection, choose Reload, or Open in Browser.");
        else SetStatus("Navigation stopped. Choose Reload or a favorite.");
    }

    private void HistoryChanged(object? sender, object e) => Changed?.Invoke();
    private void SourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
    {
        if (!_clearing && DiscordUrlPolicy.TryConversation(_view?.CoreWebView2.Source, out var conversation))
            RequestedDestination = conversation;
        Changed?.Invoke();
    }

    private void NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (!e.IsUserInitiated || !Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri)) return;
        if (DiscordUrlPolicy.IsDiscord(uri))
            host.Dispatcher.BeginInvoke(new Action(async () => await NavigateAsync(uri)));
        else if (DiscordUrlPolicy.IsExternalWeb(uri)) QueueExternal(uri);
    }

    private void PermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        // Text-chat milestone: no camera, microphone, location or notification grants.
        // Clipboard permission follows the browser's normal user-consent behavior.
        if (e.PermissionKind != CoreWebView2PermissionKind.ClipboardRead)
            e.State = CoreWebView2PermissionState.Deny;
    }

    private void DownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        e.Cancel = true;
        QueueStatus("Use Open in Browser to download attachments. Downloads and calls are outside this text-chat milestone.");
    }

    private void ProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        _recreate = true;
        Fail("The embedded browser stopped unexpectedly. Choose Reload to restart it, or Open in Browser.");
    }

    private void QueueExternal(Uri uri) => host.Dispatcher.BeginInvoke(new Action(() =>
    {
        if (!_disposed && !_clearing && ExternalBrowser.Open(uri) is { } warning) Fail(warning);
    }));

    private void QueueStatus(string message) => host.Dispatcher.BeginInvoke(new Action(() =>
    {
        if (!_disposed) SetStatus(message);
    }));

    private void Fail(string message) => SetStatus(message, error: true);
    private void SetStatus(string message, bool busy = false, bool error = false)
    {
        if (_disposed) return;
        Status = message;
        IsBusy = busy;
        HasError = error;
        Changed?.Invoke();
    }

    private void ResetBrowser()
    {
        if (_view is not null)
        {
            _view.Dispose();
            host.Content = null;
            _view = null;
        }
        _initializing = null;
        _recreate = false;
    }

    public void Dispose()
    {
        host.Dispatcher.VerifyAccess();
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        ResetBrowser();
        _lifetime.Dispose();
    }

    private static bool IsBrowserError(Exception ex) => ex is COMException or InvalidOperationException or IOException
        or UnauthorizedAccessException or ArgumentException;
}
