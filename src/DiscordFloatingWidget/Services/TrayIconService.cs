using System.ComponentModel;
using System.Drawing;
using System.Windows;
using DiscordFloatingWidget.Views;
using Forms = System.Windows.Forms;

namespace DiscordFloatingWidget.Services;

internal sealed class TrayIconService : IDisposable
{
    private readonly MainWindow _window;
    private readonly StartupService _startup;
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly Icon _image;
    private bool _disposed;

    internal Forms.ToolStripMenuItem VisibilityItem { get; } = new();
    internal Forms.ToolStripMenuItem StartupItem { get; } = new("Start with Windows");
    internal Forms.ToolStripMenuItem ExitItem { get; } = new("Exit");

    internal TrayIconService(MainWindow window, StartupService startup)
    {
        _window = window;
        _startup = startup;
        window.Dispatcher.VerifyAccess();
        _image = (Icon)SystemIcons.Application.Clone();
        _menu = new Forms.ContextMenuStrip { ShowItemToolTips = true };
        _menu.Items.AddRange([VisibilityItem, StartupItem, new Forms.ToolStripSeparator(), ExitItem]);
        _menu.Opening += MenuOpening;
        VisibilityItem.Click += ToggleVisibility;
        StartupItem.Click += ToggleStartup;
        ExitItem.Click += Exit;
        _icon = new Forms.NotifyIcon
        {
            Icon = _image,
            Text = "Discord Widget",
            ContextMenuStrip = _menu
        };
        _icon.MouseDoubleClick += IconDoubleClick;
        try
        {
            _icon.Visible = true;
            _window.CloseToTray = true;
            RefreshMenu();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal void RefreshMenu()
    {
        VisibilityItem.Text = _window.IsVisible && _window.WindowState != WindowState.Minimized
            ? "Hide Widget" : "Show Widget";
        VisibilityItem.Enabled = StartupItem.Enabled = ExitItem.Enabled = !_window.IsExiting && _window.IsEnabled;
        var (enabled, warning) = _startup.Read();
        StartupItem.Checked = enabled;
        StartupItem.Enabled &= warning is null;
        StartupItem.ToolTipText = warning ?? "Automatically open the widget when you sign in to Windows.";
    }

    private void MenuOpening(object? sender, CancelEventArgs e) => RefreshMenu();

    private void ToggleVisibility(object? sender, EventArgs e) => Queue(_window.ToggleVisibility);

    private void IconDoubleClick(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left) Queue(_window.ShowAndFocus);
    }

    private void Exit(object? sender, EventArgs e) => Queue(_window.RequestExit);

    private void ToggleStartup(object? sender, EventArgs e) => Queue(() =>
    {
        var (enabled, readWarning) = _startup.Read();
        var warning = readWarning ?? _startup.SetEnabled(!enabled);
        RefreshMenu();
        if (warning is not null)
        {
            _window.ShowAndFocus();
            MessageBox.Show(_window, warning, "Start with Windows", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    });

    private void Queue(Action action)
    {
        // A WinForms menu uses a native modal message loop. Finish that callback
        // before WPF changes visibility, opens a dialog, or starts saving on Exit.
        _window.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_disposed && !_window.IsExiting && _window.IsEnabled) action();
        }));
    }

    public void Dispose()
    {
        _window.Dispatcher.VerifyAccess();
        if (_disposed) return;
        _disposed = true;
        _window.CloseToTray = false;
        _icon.Visible = false;
        _icon.MouseDoubleClick -= IconDoubleClick;
        _icon.Dispose();
        _menu.Opening -= MenuOpening;
        VisibilityItem.Click -= ToggleVisibility;
        StartupItem.Click -= ToggleStartup;
        ExitItem.Click -= Exit;
        _menu.Dispose();
        _image.Dispose();
    }
}
