using System.ComponentModel;
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
    private bool _closing, _allowClose;
    private IInputElement? _lastFocusedElement;

    public MainWindow(SettingsService settings, WindowLayout? initialLayout, string? loadWarning)
    {
        InitializeComponent();
        _layout = new WindowStateService(this, settings, initialLayout);
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
        FullPlaceholder.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        CompactPlaceholder.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
        PlaceholderBadge.Visibility = ActualWidth < 340 || ActualHeight < 400 ? Visibility.Collapsed : Visibility.Visible;
        Footer.Visibility = ActualWidth < 300 || ActualHeight < 340 ? Visibility.Collapsed : Visibility.Visible;
        PlaceholderPanel.Margin = new Thickness(compact ? 6 : 16);
        PlaceholderPanel.Padding = new Thickness(compact ? 2 : 20);
    }

    private void ShowWarning(string warning)
    {
        if (_closing)
        {
            MessageBox.Show(this, warning, "Window layout", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        // Queue so a native window-message callback never opens a modal dialog.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_allowClose) MessageBox.Show(this, warning, "Window layout", MessageBoxButton.OK, MessageBoxImage.Warning);
        }));
    }

    private async void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        if (_closing) return;
        _closing = true;
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
            _lastFocusedElement = FocusManager.GetFocusedElement(this);
            Hide();
            return;
        }

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

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

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
