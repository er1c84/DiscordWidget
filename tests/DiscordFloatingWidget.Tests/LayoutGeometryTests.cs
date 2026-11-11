using DiscordFloatingWidget.Models;
using DiscordFloatingWidget.Services;
using Xunit;

namespace DiscordFloatingWidget.Tests;

public sealed class LayoutGeometryTests
{
    [Fact]
    public void FirstLaunchIsCenteredInWorkArea()
        => Assert.Equal(new PixelBounds(800, 310, 320, 420),
            LayoutGeometry.Restore(null, new PixelBounds(0, 0, 1920, 1040), 1));

    [Fact]
    public void NegativeCoordinatesOnLeftAndUpperMonitorsArePreserved()
    {
        var saved = new WindowLayout(-1800, -900, 360, 500, 1);
        Assert.Equal(new PixelBounds(-1800, -900, 360, 500),
            LayoutGeometry.Restore(saved, new PixelBounds(-1920, -1080, 1920, 1040), 1));
    }

    [Fact]
    public void RemovedMonitorPlacementIsRecoveredIntoRemainingWorkArea()
    {
        var saved = new WindowLayout(3000, -1200, 720, 700, 1.5);
        Assert.Equal(new PixelBounds(1200, 0, 720, 700),
            LayoutGeometry.Restore(saved, new PixelBounds(0, 0, 1920, 1040), 1));
    }

    [Fact]
    public void WorkAreaExcludesTaskbarAndBoundsOversizedWindows()
        => Assert.Equal(new PixelBounds(48, 0, 1872, 1040),
            LayoutGeometry.Constrain(new PixelBounds(-20, 500, 4000, 3000), new PixelBounds(48, 0, 1872, 1040), 1));

    [Theory]
    [InlineData(1, 360, 500)]
    [InlineData(1.25, 450, 625)]
    [InlineData(1.5, 540, 750)]
    [InlineData(2, 720, 1000)]
    public void LogicalSizeIsPreservedAcrossScalingChanges(double scale, int width, int height)
    {
        var result = LayoutGeometry.Restore(new WindowLayout(20, 30, 360, 500, 1), new PixelBounds(0, 0, 3840, 2100), scale);
        Assert.Equal(new PixelBounds(20, 30, width, height), result);
    }

    [Fact]
    public void TinySavedSizeGrowsToMinimum()
        => Assert.Equal(new PixelBounds(0, 0, 390, 450),
            LayoutGeometry.Restore(new WindowLayout(0, 0, 1, 1, 1), new PixelBounds(0, 0, 1920, 1040), 1.5));

    [Fact]
    public void EvenWorkAreaSmallerThanMinimumRemainsReachable()
        => Assert.Equal(new PixelBounds(0, 40, 120, 80),
            LayoutGeometry.Restore(null, new PixelBounds(0, 40, 120, 80), 2));

    [Fact]
    public void SavedPhysicalRectangleUsesOriginalDpiForMonitorSelection()
        => Assert.Equal(new PixelBounds(-1500, 50, 540, 750),
            LayoutGeometry.SavedBounds(new WindowLayout(-1500, 50, 360, 500, 1.5)));

    [Theory]
    [InlineData(double.NaN, 500, 1)]
    [InlineData(360, double.PositiveInfinity, 1)]
    [InlineData(-1, 500, 1)]
    [InlineData(360, 0, 1)]
    [InlineData(360, 500, 0)]
    [InlineData(360, 500, 100)]
    [InlineData(1e100, 500, 1)]
    public void InvalidValuesAreRejected(double width, double height, double scale)
        => Assert.False(new WindowLayout(0, 0, width, height, scale).IsValid);

    [Fact]
    public void UnreasonableCoordinatesAreRejectedWithoutIntegerOverflow()
        => Assert.False(new WindowLayout(int.MinValue, int.MaxValue, 360, 500, 1).IsValid);
}
