using System.IO;
using System.Security;
using Microsoft.Win32;

namespace DiscordFloatingWidget.Services;

// Only our named value in the current user's Run key is managed. Layout settings
// remain independent, and simply opening the app never enables automatic startup.
internal sealed class StartupService(string executablePath,
    string registryPath = @"Software\Microsoft\Windows\CurrentVersion\Run",
    string valueName = "DiscordFloatingWidget")
{
    private string Command => $"\"{Path.GetFullPath(executablePath)}\"";

    internal (bool Enabled, string? Warning) Read()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(registryPath);
            return (key?.GetValue(valueName) is not null, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            return (false, $"Windows startup settings could not be read.\n\n{ex.Message}");
        }
    }

    internal string? SetEnabled(bool enabled)
    {
        try
        {
            if (enabled)
            {
                if (!Path.IsPathFullyQualified(executablePath) || !File.Exists(executablePath) ||
                    !Path.GetExtension(executablePath).Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(executablePath).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase) ||
                    executablePath.Contains('"') || Command.Length > 260)
                    return "Launch the widget's built .exe from a permanent location before enabling Start with Windows. " +
                        "Windows requires a startup command of at most 260 characters.";

                using var key = Registry.CurrentUser.CreateSubKey(registryPath, writable: true);
                key.SetValue(valueName, Command, RegistryValueKind.String);
            }
            else
            {
                // Disabling an absent entry does not create a registry key.
                using var key = Registry.CurrentUser.OpenSubKey(registryPath, writable: true);
                key?.DeleteValue(valueName, throwOnMissingValue: false);
            }
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            return $"Start with Windows could not be {(enabled ? "enabled" : "disabled")}.\n\n{ex.Message}";
        }
    }
}
