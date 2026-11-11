using System.IO;
using System.Text.Json;

namespace DiscordFloatingWidget.Services;

internal sealed record WidgetPreferences(int Version = 1, Guid? LastFavoriteId = null, bool FocusModeEnabled = true);

// Separate from layout and favorites: a failed preference write never changes either.
internal sealed class WidgetPreferencesService(string path)
{
    private readonly SemaphoreSlim _writes = new(1, 1);
    private bool _canSave = true;
    internal static string DefaultPath => Path.Combine(Path.GetDirectoryName(FavoritesService.DefaultPath)!, "preferences.json");

    internal async Task<(WidgetPreferences Preferences, string? Warning)> LoadAsync()
    {
        try
        {
            await using var stream = File.OpenRead(path);
            if (stream.Length > 4096) throw new InvalidDataException();
            var value = await JsonSerializer.DeserializeAsync<WidgetPreferences>(stream);
            if (value is not { Version: 1 } || value.LastFavoriteId == Guid.Empty) throw new InvalidDataException();
            return (value, null);
        }
        catch (FileNotFoundException) { return (new(), null); }
        catch (DirectoryNotFoundException) { return (new(), null); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            _canSave = false;
            return (new(), "Widget preferences could not be read. The original file is preserved; changes apply only this session.");
        }
    }

    internal async Task<string?> SaveAsync(WidgetPreferences preferences)
    {
        if (!_canSave) return "Preferences are read-only this session because the original file could not be read.";
        if (preferences.Version != 1 || preferences.LastFavoriteId == Guid.Empty) return "Invalid widget preferences.";
        await _writes.WaitAsync().ConfigureAwait(false);
        string? temporary = null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(preferences)).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return "Widget preferences could not be saved. Your previous preferences are unchanged."; }
        finally
        {
            if (temporary is not null)
            {
                try { File.Delete(temporary); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
            _writes.Release();
        }
    }
}
