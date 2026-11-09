using System.Windows;
using System.Windows.Interop;
using DiscordFloatingWidget.Services;
using DiscordFloatingWidget.Views;

namespace DiscordFloatingWidget;

public partial class App : Application
{
    private HotkeyService? _hotkey;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var settings = new SettingsService(SettingsService.DefaultPath);
        var (layout, warning) = await settings.LoadAsync();
        var window = new MainWindow(settings, layout, warning);
        MainWindow = window;
        window.SourceInitialized += (_, _) =>
        {
            var source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle)!;
            _hotkey = new HotkeyService(source, window.ToggleVisibility);
            if (_hotkey.Register() is { } registrationWarning)
                window.Dispatcher.BeginInvoke(new Action(() => MessageBox.Show(window,
                    "Ctrl + Shift + D is unavailable.\n\n" + registrationWarning,
                    "Global shortcut unavailable", MessageBoxButton.OK, MessageBoxImage.Warning)));
        };
        // The first close is canceled while layout is saved; disable the shortcut
        // immediately so it cannot hide the window or a shutdown warning mid-close.
        window.Closing += (_, _) => _hotkey?.Dispose();
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkey?.Dispose();
        base.OnExit(e);
    }
}
