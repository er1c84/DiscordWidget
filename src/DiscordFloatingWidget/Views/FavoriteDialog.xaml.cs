using System.Windows;
using DiscordFloatingWidget.Models;

namespace DiscordFloatingWidget.Views;

public partial class FavoriteDialog : Window
{
    private readonly Guid _id;
    public FavoriteConversation? Result { get; private set; }

    public FavoriteDialog(Window owner, FavoriteConversation? favorite = null)
    {
        InitializeComponent();
        Owner = owner;
        // Existing channel favorites remain editable, but new favorites are DMs.
        if (favorite?.Type != ConversationType.ServerChannel) TypeBox.Items.RemoveAt(2);
        _id = favorite?.Id ?? Guid.NewGuid();
        if (favorite is not null)
        {
            Title = "Edit favorite";
            NameBox.Text = favorite.Name;
            LinkBox.Text = favorite.Url;
            TypeBox.SelectedIndex = (int)favorite.Type;
        }
        Loaded += (_, _) => NameBox.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!FavoriteConversation.TryCreate(_id, NameBox.Text, (ConversationType)TypeBox.SelectedIndex,
            LinkBox.Text, out var result, out var error))
        {
            ErrorText.Text = error;
            return;
        }
        Result = result;
        DialogResult = true;
    }
}
