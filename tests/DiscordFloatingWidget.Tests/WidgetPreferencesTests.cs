using System.IO;
using DiscordFloatingWidget.Services;
using Xunit;

namespace DiscordFloatingWidget.Tests;

public sealed class WidgetPreferencesTests
{
    [Fact]
    public async Task LastFavoriteAndFallbackChoiceSurviveRestart()
    {
        var path = Path.Combine(Path.GetTempPath(), "DiscordWidgetTests", Guid.NewGuid().ToString("N"), "preferences.json");
        try
        {
            var store = new WidgetPreferencesService(path);
            Assert.Equal((new WidgetPreferences(), null), await store.LoadAsync());
            var expected = new WidgetPreferences(1, Guid.NewGuid(), false);
            Assert.Null(await store.SaveAsync(expected));
            Assert.Equal((expected, null), await new WidgetPreferencesService(path).LoadAsync());
        }
        finally { if (Directory.Exists(Path.GetDirectoryName(path))) Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"Version\":2}")]
    [InlineData("{\"Version\":1,\"LastFavoriteId\":\"00000000-0000-0000-0000-000000000000\"}")]
    public async Task InvalidPreferencesArePreserved(string original)
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, original, TestContext.Current.CancellationToken);
            var store = new WidgetPreferencesService(path);
            Assert.NotNull((await store.LoadAsync()).Warning);
            Assert.NotNull(await store.SaveAsync(new()));
            Assert.Equal(original, await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        }
        finally { File.Delete(path); }
    }
}
