using System.IO;
using DiscordFloatingWidget.Models;
using DiscordFloatingWidget.Services;
using Xunit;

namespace DiscordFloatingWidget.Tests;

public sealed class FavoritesTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "DiscordWidgetTests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_directory, "favorites.json");

    [Fact]
    public async Task FavoritesSurviveAddEditRemoveAndFailedValidation()
    {
        var store = new FavoritesService(FilePath);
        Assert.Empty((await store.LoadAsync()).Favorites);
        var first = Create("A friend", ConversationType.DirectMessage, "https://discord.com/channels/@me/123");
        var second = Create("Study group", ConversationType.ServerChannel, "https://discord.com/channels/456/789");
        Assert.Null(await store.SaveAsync([first, second]));
        var (loaded, warning) = await new FavoritesService(FilePath).LoadAsync();
        Assert.Null(warning);
        Assert.Equal(new[] { first, second }, loaded);
        var edited = first with { Name = "Renamed friend" };
        Assert.Null(await store.SaveAsync([edited, second]));
        var previous = await File.ReadAllTextAsync(FilePath, TestContext.Current.CancellationToken);
        Assert.NotNull(await store.SaveAsync([edited, second with { Name = edited.Name.ToUpperInvariant() }]));
        Assert.Equal(previous, await File.ReadAllTextAsync(FilePath, TestContext.Current.CancellationToken));
        Assert.NotNull(await store.SaveAsync([edited, edited with { Name = "Duplicate ID" }]));
        Assert.Null(await store.SaveAsync([second]));
        Assert.Equal(second, Assert.Single((await store.LoadAsync()).Favorites));
        Assert.Null(await store.SaveAsync([]));
        Assert.Empty((await store.LoadAsync()).Favorites);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"Version\":2,\"Favorites\":[]}")]
    [InlineData("{\"Version\":1,\"Favorites\":[null]}")]
    [InlineData("{\"Version\":1,\"Favorites\":null}")]
    public async Task MalformedOrFutureDataIsPreservedAndEditingDisabled(string contents)
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(FilePath, contents, TestContext.Current.CancellationToken);
        var store = new FavoritesService(FilePath);
        var (loaded, warning) = await store.LoadAsync();
        Assert.Empty(loaded);
        Assert.NotNull(warning);
        Assert.False(store.CanSave);
        Assert.NotNull(await store.SaveAsync([]));
        Assert.Equal(contents, await File.ReadAllTextAsync(FilePath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OversizedDataIsPreservedAndWriteFailureDoesNotClaimSuccess()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(FilePath, new string('x', 256 * 1024 + 1), TestContext.Current.CancellationToken);
        var store = new FavoritesService(FilePath);
        Assert.NotNull((await store.LoadAsync()).Warning);
        Assert.Equal(256 * 1024 + 1, new FileInfo(FilePath).Length);
        var invalidParent = new FavoritesService(Path.Combine(FilePath, "favorites.json"));
        Assert.NotNull(await invalidParent.SaveAsync([]));
    }

    [Theory]
    [InlineData("", ConversationType.DirectMessage, "https://discord.com/channels/@me/123")]
    [InlineData("Friend", ConversationType.DirectMessage, "https://discord.com/channels/456/123")]
    [InlineData("Channel", ConversationType.ServerChannel, "https://discord.com/channels/@me/123")]
    [InlineData("Friend", (ConversationType)99, "https://discord.com/channels/@me/123")]
    [InlineData("Bad\nname", ConversationType.DirectMessage, "https://discord.com/channels/@me/123")]
    public void InvalidNamesTypesAndMismatchedLinksAreRejected(string name, ConversationType type, string url)
    {
        Assert.False(FavoriteConversation.TryCreate(Guid.NewGuid(), name, type, url, out var result, out var warning));
        Assert.Null(result);
        Assert.NotNull(warning);
    }

    internal static FavoriteConversation Create(string name, ConversationType type, string url)
    {
        Assert.True(FavoriteConversation.TryCreate(Guid.NewGuid(), name, type, url, out var favorite, out _));
        return favorite!;
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
