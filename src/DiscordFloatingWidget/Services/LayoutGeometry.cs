using DiscordFloatingWidget.Models;

namespace DiscordFloatingWidget.Services;

public readonly record struct PixelBounds(int Left, int Top, int Width, int Height);

public static class LayoutGeometry
{
    public const double MinimumWidth = 260;
    public const double MinimumHeight = 300;

    public static PixelBounds SavedBounds(WindowLayout layout) => new(layout.Left, layout.Top,
        (int)Math.Round(layout.Width * layout.DpiScale), (int)Math.Round(layout.Height * layout.DpiScale));

    public static PixelBounds Restore(WindowLayout? layout, PixelBounds workArea, double scale)
    {
        var width = (int)Math.Round((layout?.Width ?? 320) * scale);
        var height = (int)Math.Round((layout?.Height ?? 420) * scale);
        var bounds = new PixelBounds(layout?.Left ?? workArea.Left + (workArea.Width - width) / 2,
            layout?.Top ?? workArea.Top + (workArea.Height - height) / 2, width, height);
        return Constrain(bounds, workArea, scale);
    }

    public static PixelBounds Constrain(PixelBounds bounds, PixelBounds workArea, double scale)
    {
        var width = Math.Clamp(bounds.Width, Math.Min((int)Math.Ceiling(MinimumWidth * scale), workArea.Width), workArea.Width);
        var height = Math.Clamp(bounds.Height, Math.Min((int)Math.Ceiling(MinimumHeight * scale), workArea.Height), workArea.Height);
        return new PixelBounds(Math.Clamp(bounds.Left, workArea.Left, workArea.Left + workArea.Width - width),
            Math.Clamp(bounds.Top, workArea.Top, workArea.Top + workArea.Height - height), width, height);
    }
}
