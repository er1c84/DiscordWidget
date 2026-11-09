using System.IO;
using DiscordFloatingWidget.Services;
using Microsoft.Win32;
using Xunit;

namespace DiscordFloatingWidget.Tests;

public sealed class StartupServiceTests : IDisposable
{
    private readonly string _keyPath = @"Software\DiscordFloatingWidget.Tests\" + Guid.NewGuid().ToString("N");
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "DiscordWidgetTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void StartupIsOptInQuotesExecutableAndPreservesUnrelatedEntries()
    {
        Directory.CreateDirectory(_directory);
        var executable = Path.Combine(_directory, "Widget with spaces.exe");
        File.WriteAllText(executable, "Test fixture; never executed.");
        var startup = new StartupService(executable, _keyPath);
        Assert.Equal((false, null), startup.Read());
        using (var absent = Registry.CurrentUser.OpenSubKey(_keyPath)) Assert.Null(absent);

        using var key = Registry.CurrentUser.CreateSubKey(_keyPath);
        key.SetValue("AnotherApp", "keep me");
        Assert.Null(startup.SetEnabled(true));
        Assert.Equal($"\"{executable}\"", key.GetValue("DiscordFloatingWidget"));
        Assert.Equal(RegistryValueKind.String, key.GetValueKind("DiscordFloatingWidget"));
        Assert.Null(startup.SetEnabled(true));
        Assert.Equal((true, null), new StartupService(executable, _keyPath).Read());

        Assert.Null(startup.SetEnabled(false));
        Assert.Null(startup.SetEnabled(false));
        Assert.Equal((false, null), startup.Read());
        Assert.Null(key.GetValue("DiscordFloatingWidget"));
        Assert.Equal("keep me", key.GetValue("AnotherApp"));
    }

    [Fact]
    public void DisablingAbsentStartupDoesNotCreateAKey()
    {
        var startup = new StartupService(Path.Combine(_directory, "widget.exe"), _keyPath);
        Assert.Null(startup.SetEnabled(false));
        using var key = Registry.CurrentUser.OpenSubKey(_keyPath);
        Assert.Null(key);
    }

    [Theory]
    [InlineData("dotnet.exe")]
    [InlineData("widget.dll")]
    [InlineData("missing.exe")]
    public void InvalidLaunchTargetsCannotBeRegistered(string name)
    {
        Directory.CreateDirectory(_directory);
        var target = Path.Combine(_directory, name);
        if (name != "missing.exe") File.WriteAllText(target, "Test fixture; never executed.");
        var startup = new StartupService(target, _keyPath);
        Assert.NotNull(startup.SetEnabled(true));
        Assert.Equal((false, null), startup.Read());
    }

    public void Dispose()
    {
        // These are unique test keys, never the user's real Windows Run key.
        Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
