using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Threading;
using DiscordFloatingWidget.Models;
using DiscordFloatingWidget.Services;

namespace DiscordFloatingWidget.Views;

public partial class MainWindow : Window
{
    private readonly WindowStateService _layout;
    private Size? _compactSize;
    private bool _closing, _allowClose, _exitRequested, _sessionEnding;
    private IInputElement? _lastFocusedElement;

    // Enabled only after a tray icon exists, so a tray failure cannot strand the app.
    private bool _closeToTray;
    internal bool CloseToTray
    {
        get => _closeToTray;
        set
        {
            _closeToTray = value;
            var label = value ? "Hide to system tray" : "Close application";
            CloseButton.ToolTip = label;
            AutomationProperties.SetName(CloseButton, label);
        }
    }
    internal bool IsExiting => _closing;
    internal event Action? Exiting;
    internal DiscordView ContentView => DiscordContent;

    public MainWindow(SettingsService settings, WindowLayout? initialLayout, string? loadWarning,
        FavoritesService? favorites = null, string? browserProfileDirectory = null)
    {
        InitializeComponent();
        if (favorites is not null || browserProfileDirectory is not null)
            DiscordContent.Configure(favorites ?? new FavoritesService(FavoritesService.DefaultPath),
                browserProfileDirectory ?? DiscordWebService.DefaultProfileDirectory);
        _layout = new WindowStateService(this, settings, initialLayout);
        DiscordContent.ExpandRequested += ExpandForDiscord;
        _layout.Warning += ShowWarning;
        SizeChanged += (_, _) => UpdateResponsiveLayout();
        Closing += WindowClosing;
        if (loadWarning is not null)
        {
            ContentRendered += ShowLoadWarning;
            void ShowLoadWarning(object? sender, EventArgs e)
            {
                ContentRendered -= ShowLoadWarning;
                ShowWarning(loadWarning);
            }
        }
    }

    private void UpdateResponsiveLayout()
    {
        var compact = ActualWidth < 260 || ActualHeight < 280;
        HeaderTitle.Text = ActualWidth < 300 ? "DW" : "Discord Floating Widget";
        DiscordContent.IsCompact = compact;
        Footer.Visibility = ActualWidth < 300 || ActualHeight < 340 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ShowWarning(string warning)
    {
        if (_sessionEnding)
        {
            Trace.TraceWarning(warning);
            return;
        }
        if (_closing)
        {
            if (!IsVisible) Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
            MessageBox.Show(this, warning, "Discord Widget", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        // Queue so a native window-message callback never opens a modal dialog.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_allowClose) MessageBox.Show(this, warning, "Discord Widget", MessageBoxButton.OK, MessageBoxImage.Warning);
        }));
    }

    private async void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        if (_closing) return;
        if (CloseToTray && !_exitRequested)
        {
            HideToTray();
            return;
        }
        _closing = true;
        Exiting?.Invoke();
        if (await DiscordContent.StopAsync() is { } favoritesWarning) ShowWarning(favoritesWarning);
        await _layout.StopAndSaveAsync();
        _allowClose = true;
        Close();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    public void ToggleVisibility()
    {
        // WPF disables an owner while a modal dialog is open. Keep it reachable.
        if (_closing || !IsEnabled) return;
        if (IsVisible && WindowState != WindowState.Minimized)
        {
            HideToTray();
            return;
        }

        ShowAndFocus();
    }

    internal void HideToTray()
    {
        if (_closing || !IsEnabled) return;
        _lastFocusedElement = FocusManager.GetFocusedElement(this) ?? _lastFocusedElement;
        Hide();
    }

    internal void ShowAndFocus()
    {
        if (_closing || !IsEnabled) return;

        _lastFocusedElement = FocusManager.GetFocusedElement(this) ?? _lastFocusedElement;
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        _layout.RecoverVisibleLayout();
        if (Activate())
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (IsVisible && IsActive && !_closing)
                {
                    if (_lastFocusedElement is UIElement { IsVisible: true, IsEnabled: true } element)
                    {
                        FocusManager.SetFocusedElement(this, element);
                        Keyboard.Focus(element);
                    }
                    else
                        MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
                }
            }));
        }
    }

    internal void RequestExit()
    {
        if (_closing || !IsEnabled) return;
        _exitRequested = true;
        Close();
    }

    internal void PrepareForSessionEnd()
    {
        _sessionEnding = true;
        _exitRequested = _closing = true;
        Exiting?.Invoke();
        if (DiscordContent.StopForSessionEnd() is { } favoritesWarning) Trace.TraceWarning(favoritesWarning);
        if (_layout.StopAndSaveForSessionEnd() is { } warning) Trace.TraceWarning(warning);
        _allowClose = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    internal Task ClearBrowserDataAsync() => DiscordContent.ConfirmClearBrowserDataAsync();

    private void ExpandForDiscord()
    {
        if (_compactSize is null) _compactSize = new Size(ActualWidth, ActualHeight);
        _layout.Resize(Math.Max(ActualWidth, 720), Math.Max(ActualHeight, 700));
        ExpandButton.ToolTip = "Restore compact size";
        AutomationProperties.SetName(ExpandButton, "Restore compact size");
    }

    private void Expand_Click(object sender, RoutedEventArgs e)
    {
        if (_compactSize is Size compact)
        {
            _layout.Resize(compact.Width, compact.Height);
            _compactSize = null;
            ExpandButton.ToolTip = "Expand";
            AutomationProperties.SetName(ExpandButton, "Expand");
        }
        else
        {
            _compactSize = new Size(ActualWidth, ActualHeight);
            _layout.Resize(Math.Max(ActualWidth, 720), Math.Max(ActualHeight, 700));
            ExpandButton.ToolTip = "Restore compact size";
            AutomationProperties.SetName(ExpandButton, "Restore compact size");
        }
    }
}
