using System.Text.Json.Serialization;

namespace DiscordFloatingWidget.Models;

// Desktop coordinates are physical pixels; sizes are WPF device-independent units.
public sealed record WindowLayout(int Left, int Top, double Width, double Height, double DpiScale)
{
    [JsonIgnore]
    public bool IsValid => Math.Abs((long)Left) <= 1_000_000 && Math.Abs((long)Top) <= 1_000_000
        && double.IsFinite(Width) && Width is > 0 and <= 100_000
        && double.IsFinite(Height) && Height is > 0 and <= 100_000
        && double.IsFinite(DpiScale) && DpiScale is >= 0.5 and <= 8;
}
