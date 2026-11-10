using System.Globalization;

namespace DiscordFloatingWidget.Services;

internal static class DiscordUrlPolicy
{
    internal static Uri Home { get; } = new("https://discord.com/app");

    internal static bool IsDiscord(Uri uri) => uri.IsAbsoluteUri && uri.Scheme == Uri.UriSchemeHttps &&
        uri.IdnHost.Equals("discord.com", StringComparison.OrdinalIgnoreCase) && uri.IsDefaultPort && uri.UserInfo.Length == 0;

    internal static bool IsExternalWeb(Uri uri) => uri.IsAbsoluteUri &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) && uri.UserInfo.Length == 0;

    internal static bool TryConversation(string? text, out Uri destination)
    {
        destination = Home;
        if (string.IsNullOrWhiteSpace(text) || text.Length > 2048 ||
            !Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri) || !IsDiscord(uri)) return false;
        var parts = uri.AbsolutePath.TrimEnd('/').Split('/');
        if (parts.Length is not (4 or 5) || parts[1] != "channels" ||
            (parts[2] != "@me" && !IsId(parts[2])) || !IsId(parts[3]) ||
            (parts.Length == 5 && !IsId(parts[4]))) return false;
        // A copied message link becomes a conversation shortcut. Never persist
        // fragments, query strings, authentication routes, or arbitrary web URLs.
        destination = new Uri($"https://discord.com/channels/{parts[2]}/{parts[3]}");
        return true;
    }

    internal static Uri BrowserDestination(Uri? current, Uri requested) =>
        TryConversation(current?.AbsoluteUri, out var conversation) ? conversation :
        TryConversation(requested.AbsoluteUri, out var requestedConversation) ? requestedConversation : Home;

    private static bool IsId(string value) => value.Length <= 20 &&
        value.All(char.IsAsciiDigit) && ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id != 0;
}
