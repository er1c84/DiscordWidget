using DiscordFloatingWidget.Services;
using Xunit;

namespace DiscordFloatingWidget.Tests;

public sealed class DiscordUrlPolicyTests
{
    [Theory]
    [InlineData("https://discord.com/channels/@me/123", "https://discord.com/channels/@me/123")]
    [InlineData("https://discord.com/channels/123/456/789?context=message#fragment", "https://discord.com/channels/123/456")]
    [InlineData(" https://DISCORD.com/channels/@me/123/ ", "https://discord.com/channels/@me/123")]
    public void ConversationLinksAreCanonicalizedWithoutQueryOrMessageData(string input, string expected)
    {
        Assert.True(DiscordUrlPolicy.TryConversation(input, out var uri));
        Assert.Equal(expected, uri.AbsoluteUri);
    }

    [Theory]
    [InlineData("https://discord.com/login?token=example")]
    [InlineData("https://discord.com/app")]
    [InlineData("https://discord.com/channels/@me")]
    [InlineData("https://discord.com/channels/@me/0")]
    [InlineData("https://discord.com/channels/123/18446744073709551616")]
    [InlineData("https://discord.com/channels/@me/friend-name")]
    [InlineData("http://discord.com/channels/@me/123")]
    [InlineData("https://discord.com:444/channels/@me/123")]
    [InlineData("https://discord.com.evil.example/channels/@me/123")]
    [InlineData("https://discord.com@evil.example/channels/@me/123")]
    [InlineData("https://user:password@discord.com/channels/@me/123")]
    [InlineData("https://canary.discord.com/channels/@me/123")]
    [InlineData("file:///C:/windows/example")]
    [InlineData("javascript:alert(1)")]
    [InlineData("discord://channels/@me/123")]
    public void UnsafeOrNonConversationFavoritesAreRejected(string input) =>
        Assert.False(DiscordUrlPolicy.TryConversation(input, out _));

    [Theory]
    [InlineData("file:///C:/example")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,test")]
    [InlineData("discord://channels/@me/123")]
    [InlineData("mailto:test@example.com")]
    [InlineData("https://user:password@example.com/")]
    public void BrowserFallbackCannotLaunchOtherProtocolsOrCredentialUrls(string input) =>
        Assert.False(DiscordUrlPolicy.IsExternalWeb(new Uri(input)));

    [Fact]
    public void BrowserFallbackNeverExportsAuthenticationRoutesOrQueries()
    {
        var requested = new Uri("https://discord.com/channels/@me/123");
        Assert.Equal(requested, DiscordUrlPolicy.BrowserDestination(new Uri("https://discord.com/login?token=example"), requested));
        Assert.Equal(DiscordUrlPolicy.Home, DiscordUrlPolicy.BrowserDestination(null, new Uri("https://discord.com/login?token=example")));
        Assert.Equal(new Uri("https://discord.com/channels/456/789"),
            DiscordUrlPolicy.BrowserDestination(new Uri("https://discord.com/channels/456/789?secret=example"), requested));
        Assert.True(DiscordUrlPolicy.IsDiscord(new Uri("https://discord.com/login")));
        Assert.True(DiscordUrlPolicy.IsExternalWeb(new Uri("https://example.com/")));
    }
}
