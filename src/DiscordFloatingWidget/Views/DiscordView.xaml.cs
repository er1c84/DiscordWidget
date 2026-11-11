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
    private WidgetPreferencesService _preferences = new(WidgetPreferencesService.DefaultPath);
    private WidgetPreferences _preference = new();
    private readonly ObservableCollection<FavoriteConversation> _items = [];
    private readonly ObservableCollection<FavoriteConversation> _quickItems = [];
    private Task? _loading;
    private Task<string?>? _pendingSave;
    private Task<string?>? _pendingPreferenceSave;
    private bool _disposed, _showBrowser, _mutating, _resetting, _loaded, _selecting, _changingFocus;
    private string? _notice;

    internal event Action? CompactSizeRequested;
    internal event Action<string>? ConversationChanged;
    internal IReadOnlyList<FavoriteConversation> Favorites => _items;
    internal Guid? LastFavoriteId => _preference.LastFavoriteId;
    internal DiscordWebService Browser => _browser;

    public DiscordView()
    {
        InitializeComponent();
        FavoritesList.ItemsSource = _items;
        _browser = new DiscordWebService(BrowserHost, DiscordWebService.DefaultProfileDirectory);
        _browser.Changed += Refresh;
        Loaded += async (_, _) => await LoadFavoritesAsync();
    }

    internal void Configure(FavoritesService favorites, string profileDirectory)
    {
        if (_loading is not null) throw new InvalidOperationException("Configure before the view is loaded.");
        _browser.Dispose();
        _favorites = favorites;
        _preferences = new WidgetPreferencesService(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(profileDirectory)!, "preferences.json"));
        _browser = new DiscordWebService(BrowserHost, profileDirectory);
        _browser.Changed += Refresh;
    }

    internal Task LoadFavoritesAsync() => _loading ??= LoadAsync();
    private async Task LoadAsync()
    {
        var (favorites, warning) = await _favorites.LoadAsync();
        var (preferences, preferenceWarning) = await _preferences.LoadAsync();
        if (_disposed) return;
        _preference = preferences;
        await _browser.SetFocusEnabledAsync(_preference.FocusModeEnabled);
        if (_disposed) return;
        _loaded = true;
        ReplaceItems(favorites);
        _notice = warning ?? preferenceWarning;
        Refresh();
        if (_quickItems.FirstOrDefault(item => item.Id == _preference.LastFavoriteId) is { } last)
            await OpenFavoriteAsync(last);
    }

    private void Refresh()
    {
        if (_disposed) return;
        PickerPanel.Visibility = !_showBrowser ? Visibility.Visible : Visibility.Collapsed;
        BrowserHost.Visibility = _showBrowser ? Visibility.Visible : Visibility.Collapsed;
        BackButton.IsEnabled = _showBrowser && _browser.CanGoBack && !_resetting;
        ReloadButton.IsEnabled = _showBrowser && !_resetting;
        MoreButton.IsEnabled = _loaded && !_resetting && !_mutating && !_changingFocus;
        AddButton.IsEnabled = _loaded && _favorites.CanSave && !_mutating && !_resetting;
        QuickAddButton.IsEnabled = AddButton.IsEnabled;
        NavigationTabs.IsEnabled = _loaded && !_resetting && !_mutating && !_changingFocus;
        FocusMenu.IsChecked = _preference.FocusModeEnabled;
        FavoritesList.IsEnabled = !_mutating && !_resetting;
        EmptyText.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var message = _notice ?? (_showBrowser && (_browser.IsBusy || _browser.HasError || _browser.HasNotice) ? _browser.Status : null);
        StatusText.Text = message;
        StatusText.Foreground = (System.Windows.Media.Brush)FindResource(_browser.HasError ? "WarningBrush" : "MutedBrush");
        StatusPanel.Visibility = !string.IsNullOrEmpty(message) ? Visibility.Visible : Visibility.Collapsed;
        RuntimeButton.Visibility = _browser.RuntimeMissing ? Visibility.Visible : Visibility.Collapsed;
        var current = _showBrowser ? _items.FirstOrDefault(item => item.Url == _browser.RequestedDestination.AbsoluteUri) : null;
        _selecting = true;
        NavigationTabs.SelectedItem = _showBrowser ? DiscordTab : FavoritesTab;
        _selecting = false;
        ConversationChanged?.Invoke(_showBrowser ? current?.Name ?? "Discord" : "Your favorites");
    }

    private void ReplaceItems(IEnumerable<FavoriteConversation> items)
    {
        _selecting = true;
        _items.Clear();
        _quickItems.Clear();
        foreach (var item in items)
        {
            _items.Add(item);
            if (item.Type != ConversationType.ServerChannel) _quickItems.Add(item);
        }
        _selecting = false;
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
        if (sender is Button { Tag: FavoriteConversation favorite }) await OpenFavoriteAsync(favorite);
    }

    private async void NavigationTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_selecting || _disposed || !_loaded || _resetting || !ReferenceEquals(e.Source, NavigationTabs)) return;
        if (NavigationTabs.SelectedItem == FavoritesTab)
        {
            Favorites_Click(sender, e);
        }
        else if (!_showBrowser)
        {
            if (_browser.IsReady)
            {
                _showBrowser = true;
                _notice = null;
                Refresh();
            }
            else await OpenAsync(_browser.RequestedDestination);
        }
    }

    internal async Task OpenFavoriteAsync(FavoriteConversation favorite)
    {
        if (_disposed || _resetting || !DiscordUrlPolicy.TryConversation(favorite.Url, out var uri)) return;
        _preference = _preference with { LastFavoriteId = favorite.Id };
        var saving = SavePreferencesAsync();
        await OpenAsync(uri);
        await saving;
    }

    private async Task SavePreferencesAsync()
    {
        _pendingPreferenceSave = _preferences.SaveAsync(_preference);
        var warning = await _pendingPreferenceSave;
        if (!_disposed && warning is not null) { _notice = warning; Refresh(); }
    }

    private async void Focus_Click(object sender, RoutedEventArgs e)
    {
        if (_changingFocus || _resetting) return;
        _preference = _preference with { FocusModeEnabled = FocusMenu.IsChecked };
        _changingFocus = true;
        Refresh();
        try { await _browser.SetFocusEnabledAsync(_preference.FocusModeEnabled); await SavePreferencesAsync(); }
        finally { _changingFocus = false; Refresh(); }
    }

    internal void SetWindowVisible(bool visible) => _browser.SetWindowVisible(visible);

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
    private void CompactSize_Click(object sender, RoutedEventArgs e) => CompactSizeRequested?.Invoke();
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
        var warning = _pendingSave is not null ? await _pendingSave : null;
        var preferenceWarning = _pendingPreferenceSave is not null ? await _pendingPreferenceSave : null;
        return warning ?? preferenceWarning;
    }
    internal string? StopForSessionEnd()
    {
        Dispose();
        var warning = _pendingSave?.GetAwaiter().GetResult();
        var preferenceWarning = _pendingPreferenceSave?.GetAwaiter().GetResult();
        return warning ?? preferenceWarning;
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _browser.Changed -= Refresh;
        _browser.Dispose();
    }
}
