using System.IO;
using System.Text.Json;
using DiscordFloatingWidget.Models;

namespace DiscordFloatingWidget.Services;

public sealed class SettingsService(string filePath)
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _canSave = true;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), "DiscordFloatingWidget", "settings.json");

    public async Task<(WindowLayout? Layout, string? Warning)> LoadAsync()
    {
        try
        {
            await using var stream = File.OpenRead(filePath);
            if (stream.Length > 64 * 1024)
                throw new InvalidDataException("The layout file is too large.");
            var settings = await JsonSerializer.DeserializeAsync<LayoutSettings>(stream);
            if (settings is not { Version: 1, Layout.IsValid: true })
                throw new InvalidDataException("The layout file contains invalid or unsupported settings.");
            return (settings.Layout, null);
        }
        catch (FileNotFoundException) { return (null, null); }
        catch (DirectoryNotFoundException) { return (null, null); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            // Keep malformed, future-version, or unreadable data intact for inspection.
            _canSave = false;
            return (null, $"Window layout could not be loaded. Default placement will be used. " +
                $"Layout saving is disabled for this session. Close the app, then rename or remove '{filePath}' to reset it.\n\n{ex.Message}");
        }
    }

    public async Task<string?> SaveAsync(WindowLayout layout)
    {
        if (!_canSave) return null;
        if (!layout.IsValid) throw new ArgumentException("Invalid window layout.", nameof(layout));
        await _writeLock.WaitAsync();
        string? temporaryPath = null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
            temporaryPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(new LayoutSettings(1, layout), JsonOptions));
            // Replace in the same directory, so an interrupted write cannot truncate the old JSON.
            File.Move(temporaryPath, filePath, overwrite: true);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"Window layout could not be saved to '{filePath}'. Your window remains usable.\n\n{ex.Message}";
        }
        finally
        {
            if (temporaryPath is not null)
            {
                try { File.Delete(temporaryPath); }
                catch (IOException) { /* A failed write is already reported; a leftover temp file is harmless. */ }
                catch (UnauthorizedAccessException) { /* Same as above. */ }
            }
            _writeLock.Release();
        }
    }

    private sealed record LayoutSettings(int Version, WindowLayout Layout);
}
