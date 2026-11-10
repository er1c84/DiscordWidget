using System.Text.Json.Serialization;
using DiscordFloatingWidget.Services;

namespace DiscordFloatingWidget.Models;

public enum ConversationType { DirectMessage, GroupDirectMessage, ServerChannel }

public sealed record FavoriteConversation(Guid Id, string Name, ConversationType Type, string Url)
{
    [JsonIgnore]
    public string TypeLabel => Type switch
    {
        ConversationType.DirectMessage => "Friend / direct message",
        ConversationType.GroupDirectMessage => "Group direct message",
        _ => "Server channel"
    };

    public static bool TryCreate(Guid id, string? name, ConversationType type, string? url,
        out FavoriteConversation? favorite, out string? error)
    {
        favorite = null;
        error = null;
        name = name?.Trim();
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(name) || name.Length > 80 || name.Any(char.IsControl))
            error = "Enter a name between 1 and 80 characters.";
        else if (!Enum.IsDefined(type))
            error = "Choose a conversation type.";
        else if (!DiscordUrlPolicy.TryConversation(url, out var destination))
            error = "Paste a Discord conversation link, such as https://discord.com/channels/@me/123456789 or a server channel link.";
        else if ((type == ConversationType.ServerChannel) == destination.AbsolutePath.StartsWith("/channels/@me/", StringComparison.Ordinal))
            error = "Choose a DM type for a /channels/@me/ link, or Server channel for a server link.";
        else
            favorite = new(id, name, type, destination.AbsoluteUri);
        return favorite is not null;
    }
}
