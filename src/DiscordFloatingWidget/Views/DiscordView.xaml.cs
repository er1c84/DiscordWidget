using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using DiscordFloatingWidget.Models;
using DiscordFloatingWidget.Services;

namespace DiscordFloatingWidget.Views;

public partial class DiscordView : UserControl, IDisposable
{
    private FavoritesService _favorites = new(FavoritesService.DefaultPath);
    private DiscordWebService _browser;
    private readonly ObservableCollection<FavoriteConversation> _items = [];
    private Task? _loading;
    private Task<string?>? _pendingSave;
    private bool _disposed, _showBrowser, _compact, _mutating, _resetting, _loaded;
    private string? _notice;

    internal event Action? ExpandRequested;
    internal bool IsCompact { get => _compact; set { _compact = value; Refresh(); } }
    internal IReadOnlyList<FavoriteConversation> Favorites => _items;

    public DiscordView()
    {
        InitializeComponent();
        FavoritesList.ItemsSource = _items;
        _browser = new DiscordWebService(BrowserHost, DiscordWebService.DefaultProfileDirectory);
        _browser.Changed += Refresh;
        Loaded += async (_, _) => await LoadFavoritesAsync();
        SizeChanged += (_, _) => Refresh();
    }

    internal void Configure(FavoritesService favorites, string profileDirectory)
    {
        if (_loading is not null) throw new InvalidOperationException("Configure before the view is loaded.");
        _browser.Dispose();
        _favorites = favorites;
        _browser = new DiscordWebService(BrowserHost, profileDirectory);
        _browser.Changed += Refresh;
    }

    internal Task LoadFavoritesAsync() => _loading ??= LoadAsync();
    private async Task LoadAsync()
    {
        var (favorites, warning) = await _favorites.LoadAsync();
        if (_disposed) return;
        _loaded = true;
        ReplaceItems(favorites);
        _notice = warning;
        Refresh();
    }

    private void Refresh()
    {
        if (_disposed) return;
        var small = _compact || (_showBrowser && (ActualWidth < 480 || ActualHeight < 320));
        Toolbar.Visibility = small ? Visibility.Collapsed : Visibility.Visible;
        PickerPanel.Visibility = !small && !_showBrowser ? Visibility.Visible : Visibility.Collapsed;
        BrowserHost.Visibility = !small && _showBrowser ? Visibility.Visible : Visibility.Collapsed;
        CompactPanel.Visibility = small ? Visibility.Visible : Visibility.Collapsed;
        CompactLabel.Text = _showBrowser ? "Expand to view Discord" : "Your favorites";
        BackButton.IsEnabled = _showBrowser && _browser.CanGoBack && !_resetting;
        ReloadButton.IsEnabled = _showBrowser && !_resetting;
        MoreButton.IsEnabled = !_resetting && !_mutating;
        AddButton.IsEnabled = _loaded && _favorites.CanSave && !_mutating && !_resetting;
        FavoritesList.IsEnabled = !_mutating && !_resetting;
        EmptyText.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var message = _notice ?? (_showBrowser ? _browser.Status : null);
        StatusText.Text = message;
        StatusText.Foreground = (System.Windows.Media.Brush)FindResource(_browser.HasError ? "WarningBrush" : "MutedBrush");
        StatusPanel.Visibility = !small && !string.IsNullOrEmpty(message) ? Visibility.Visible : Visibility.Collapsed;
        RuntimeButton.Visibility = _browser.RuntimeMissing ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ReplaceItems(IEnumerable<FavoriteConversation> items)
    {
        _items.Clear();
        foreach (var item in items) _items.Add(item);
    }

    internal async Task<string?> SaveFavoritesAsync(IReadOnlyList<FavoriteConversation> items)
    {
        if (_disposed || _mutating) return "Favorites are busy.";
        _mutating = true;
        Refresh();
        try
        {
            _pendingSave = _favorites.SaveAsync(items);
            var warning = await _pendingSave;
            if (_disposed) return warning;
            if (warning is null) ReplaceItems(items);
            _notice = warning;
            return warning;
        }
        finally { _mutating = false; Refresh(); }
    }

    private async void Add_Click(object sender, RoutedEventArgs e) => await EditFavoriteAsync(null);
    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: FavoriteConversation favorite }) await EditFavoriteAsync(favorite);
    }

    private async Task EditFavoriteAsync(FavoriteConversation? favorite)
    {
        if (!_favorites.CanSave || _mutating || _resetting) return;
        var dialog = new FavoriteDialog(Window.GetWindow(this), favorite);
        if (dialog.ShowDialog() != true || dialog.Result is not { } result) return;
        var items = _items.ToList();
        var index = items.FindIndex(item => item.Id == result.Id);
        if (index < 0) items.Add(result); else items[index] = result;
        await SaveFavoritesAsync(items);
    }

    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: FavoriteConversation favorite } || !_favorites.CanSave || _mutating) return;
        if (MessageBox.Show(Window.GetWindow(this), $"Remove ‘{favorite.Name}’ from favorites? This does not remove anything from Discord.",
            "Remove favorite", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
            await SaveFavoritesAsync(_items.Where(item => item.Id != favorite.Id).ToList());
    }

    private async void OpenFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: FavoriteConversation favorite } && DiscordUrlPolicy.TryConversation(favorite.Url, out var uri))
            await OpenAsync(uri);
    }

    private async void OpenDiscord_Click(object sender, RoutedEventArgs e) => await OpenAsync(DiscordUrlPolicy.Home);
    private async Task OpenAsync(Uri destination)
    {
        if (_resetting || _disposed) return;
        _notice = null;
        _showBrowser = true;
        Refresh();
        await _browser.NavigateAsync(destination);
        Refresh();
    }

    private void Favorites_Click(object sender, RoutedEventArgs e)
    {
        _showBrowser = false;
        _notice = null;
        Refresh();
    }
    private void Back_Click(object sender, RoutedEventArgs e) => _browser.Back();
    private async void Reload_Click(object sender, RoutedEventArgs e)
    {
        _notice = null;
        await _browser.ReloadAsync();
        Refresh();
    }
    private void Browser_Click(object sender, RoutedEventArgs e)
    {
        if (ExternalBrowser.Open(_showBrowser ? _browser.ExternalDestination : DiscordUrlPolicy.Home) is { } warning)
        { _notice = warning; Refresh(); }
    }
    private void Runtime_Click(object sender, RoutedEventArgs e)
    {
        if (ExternalBrowser.Open(DiscordWebService.RuntimeDownload) is { } warning) { _notice = warning; Refresh(); }
    }
    private void More_Click(object sender, RoutedEventArgs e)
    {
        MoreButton.ContextMenu.PlacementTarget = MoreButton;
        MoreButton.ContextMenu.IsOpen = true;
    }
    private void Expand_Click(object sender, RoutedEventArgs e) => ExpandRequested?.Invoke();
    private void ExpandFavorites_Click(object sender, RoutedEventArgs e)
    { _showBrowser = false; ExpandRequested?.Invoke(); Refresh(); }
    private async void Clear_Click(object sender, RoutedEventArgs e) => await ConfirmClearBrowserDataAsync();

    internal async Task ConfirmClearBrowserDataAsync()
    {
        if (_disposed || _resetting || _mutating) return;
        if (MessageBox.Show(Window.GetWindow(this), "Clear this widget’s browser data? This signs you out of Discord in the widget. " +
            "Your favorites, window layout, and other browsers are kept.", "Clear browser data",
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        _resetting = true;
        _notice = "Clearing the widget’s browser data…";
        Refresh();
        try
        {
            if (await _browser.ClearDataAsync())
            { _showBrowser = false; _notice = _browser.Status; }
            else _notice = _browser.Status;
        }
        finally { _resetting = false; Refresh(); }
    }

    internal async Task<string?> StopAsync()
    {
        Dispose();
        return _pendingSave is not null ? await _pendingSave : null;
    }
    internal string? StopForSessionEnd()
    {
        Dispose();
        return _pendingSave?.GetAwaiter().GetResult();
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _browser.Changed -= Refresh;
        _browser.Dispose();
    }
}
