using System.IO;
using DiscordFloatingWidget.Models;
using DiscordFloatingWidget.Services;
using Xunit;

namespace DiscordFloatingWidget.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "DiscordWidgetTests", Guid.NewGuid().ToString("N"));
    private string SettingsPath => Path.Combine(_directory, "settings.json");

    [Fact]
    public async Task MissingFileUsesDefaultsWithoutWarning()
    {
        var (layout, warning) = await new SettingsService(SettingsPath).LoadAsync();
        Assert.Null(layout);
        Assert.Null(warning);
    }

    [Fact]
    public async Task SavesAndRestoresLayoutAndReplacesExistingFile()
    {
        var service = new SettingsService(SettingsPath);
        var first = new WindowLayout(-1200, 70, 260, 180, 1.5);
        Assert.Null(await service.SaveAsync(first));
        Assert.Equal(first, (await new SettingsService(SettingsPath).LoadAsync()).Layout);
        var second = first with { Width = 720, Height = 700 };
        Assert.Null(await service.SaveAsync(second));
        Assert.Equal(second, (await new SettingsService(SettingsPath).LoadAsync()).Layout);
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"Version\":99,\"Layout\":{\"Left\":0,\"Top\":0,\"Width\":360,\"Height\":500,\"DpiScale\":1}}")]
    [InlineData("{\"Version\":1,\"Layout\":{\"Width\":-1,\"Height\":500,\"DpiScale\":1}}")]
    public async Task InvalidSettingsProduceWarningAndAreNotOverwritten(string json)
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(SettingsPath, json, TestContext.Current.CancellationToken);
        var service = new SettingsService(SettingsPath);
        var (layout, warning) = await service.LoadAsync();
        Assert.Null(layout);
        Assert.NotNull(warning);
        await service.SaveAsync(new WindowLayout(0, 0, 360, 500, 1));
        Assert.Equal(json, await File.ReadAllTextAsync(SettingsPath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OversizedSettingsAreRejectedAndPreserved()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(SettingsPath, new string(' ', 70_000), TestContext.Current.CancellationToken);
        var (layout, warning) = await new SettingsService(SettingsPath).LoadAsync();
        Assert.Null(layout);
        Assert.NotNull(warning);
        Assert.Equal(70_000, new FileInfo(SettingsPath).Length);
    }

    [Fact]
    public async Task UnwritableDestinationReturnsActionableWarning()
    {
        Directory.CreateDirectory(_directory);
        var blocker = Path.Combine(_directory, "file-instead-of-directory");
        await File.WriteAllTextAsync(blocker, "keep", TestContext.Current.CancellationToken);
        var warning = await new SettingsService(Path.Combine(blocker, "settings.json"))
            .SaveAsync(new WindowLayout(0, 0, 360, 500, 1));
        Assert.Contains("could not be saved", warning);
        Assert.Equal("keep", await File.ReadAllTextAsync(blocker, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConcurrentSavesLeaveCompleteJsonAndNoTemporaryFiles()
    {
        var service = new SettingsService(SettingsPath);
        var layouts = Enumerable.Range(0, 20).Select(i => new WindowLayout(i * 10, i, 360 + i, 500, 1)).ToArray();
        var results = await Task.WhenAll(layouts.Select(service.SaveAsync));
        Assert.All(results, result => Assert.Null(result));
        var (saved, warning) = await new SettingsService(SettingsPath).LoadAsync();
        Assert.Null(warning);
        Assert.Contains(saved, layouts);
        Assert.Single(Directory.GetFiles(_directory));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
