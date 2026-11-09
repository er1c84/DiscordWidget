using System.Windows;
using DiscordFloatingWidget.Services;
using DiscordFloatingWidget.Views;

namespace DiscordFloatingWidget;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var settings = new SettingsService(SettingsService.DefaultPath);
        var (layout, warning) = await settings.LoadAsync();
        MainWindow = new MainWindow(settings, layout, warning);
        MainWindow.Show();
    }
}
