using System.IO;

namespace DiscordFloatingWidget.Services;

internal static class DiscordFocusScript
{
    internal static string Source { get; } = Read();
    private static string Read()
    {
        using var stream = typeof(DiscordFocusScript).Assembly.GetManifestResourceStream("DiscordFloatingWidget.Resources.DiscordFocus.js")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
