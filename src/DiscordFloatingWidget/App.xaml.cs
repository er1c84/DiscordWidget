using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using DiscordFloatingWidget.Services;
using DiscordFloatingWidget.Views;

namespace DiscordFloatingWidget;

public partial class App : Application
{
    private HotkeyService? _hotkey;
    private TrayIconService? _tray;
    private SingleInstanceService? _instance;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            // Claim the instance before awaiting settings or creating any window,
            // tray icon, or hotkey registration.
            _instance = new SingleInstanceService();
            if (!_instance.IsPrimary)
            {
                if (!await _instance.NotifyPrimaryAsync())
                    MessageBox.Show("Discord Widget is already running, but it did not respond. " +
                        "Try its tray icon or Ctrl + Shift + D. If it is still starting or exiting, wait and try again.",
                        "Discord Widget", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            var settings = new SettingsService(SettingsService.DefaultPath);
            var (layout, warning) = await settings.LoadAsync();
            var window = new MainWindow(settings, layout, warning);
            MainWindow = window;
            window.SourceInitialized += (_, _) =>
            {
                var source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle)!;
                _hotkey = new HotkeyService(source, window.ToggleVisibility);
                if (_hotkey.Register() is { } registrationWarning)
                    window.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (!window.IsExiting) MessageBox.Show(window,
                            "Ctrl + Shift + D is unavailable.\n\n" + registrationWarning,
                            "Global shortcut unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }));
            };
            window.Exiting += () =>
            {
                _hotkey?.Dispose();
                _tray?.Dispose();
            };
            window.Closed += (_, _) => Shutdown();
            window.Show();

            try { _tray = new TrayIconService(window, new StartupService(Environment.ProcessPath!)); }
            catch (Exception ex)
            {
                // Keep the visible app usable with ordinary close-to-exit if the
                // notification area cannot be initialized.
                Trace.TraceError(ex.ToString());
                MessageBox.Show(window, "The tray icon could not be created. The X button will exit the app this session.\n\n" + ex.Message,
                    "System tray unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            _instance.StartListening(() => Dispatcher.BeginInvoke(new Action(window.ShowAndFocus)),
                message => Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (window.IsExiting) return;
                    window.ShowAndFocus();
                    MessageBox.Show(window, message, "Desktop shortcut", MessageBoxButton.OK, MessageBoxImage.Warning);
                })));
        }
        catch (Exception ex)
        {
            Trace.TraceError(ex.ToString());
            MessageBox.Show("Discord Widget could not start.\n\n" + ex.Message,
                "Discord Widget", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        // Do not turn Windows sign-out/shutdown into a close-to-tray cancellation.
        if (MainWindow is MainWindow window) window.PrepareForSessionEnd();
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkey?.Dispose();
        _tray?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }
}
