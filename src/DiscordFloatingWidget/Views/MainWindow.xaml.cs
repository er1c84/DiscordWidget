using System.Windows;

namespace DiscordFloatingWidget.Views;

public partial class MainWindow : Window
{
    private Size? _compactSize;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Expand_Click(object sender, RoutedEventArgs e)
    {
        // Expanding changes the widget size instead of covering the whole desktop.
        if (WindowState != WindowState.Normal)
            WindowState = WindowState.Normal;

        if (_compactSize is Size compact)
        {
            Width = compact.Width;
            Height = compact.Height;
            _compactSize = null;
            ExpandButton.ToolTip = "Expand";
            System.Windows.Automation.AutomationProperties.SetName(ExpandButton, "Expand");
        }
        else
        {
            _compactSize = new Size(ActualWidth, ActualHeight);
            Width = Math.Max(Width, 720);
            Height = Math.Max(Height, 700);
            ExpandButton.ToolTip = "Restore compact size";
            System.Windows.Automation.AutomationProperties.SetName(ExpandButton, "Restore compact size");
        }
    }
}
