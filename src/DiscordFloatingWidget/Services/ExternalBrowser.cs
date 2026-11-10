using System.ComponentModel;
using System.Diagnostics;

namespace DiscordFloatingWidget.Services;

internal static class ExternalBrowser
{
    internal static string? Open(Uri uri)
    {
        if (!DiscordUrlPolicy.IsExternalWeb(uri)) return "Only ordinary HTTP and HTTPS links can open in your browser.";
        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            return null;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return "Windows could not open your default browser. Check your default browser setting and try again.";
        }
    }
}
