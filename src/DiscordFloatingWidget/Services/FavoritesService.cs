using System.IO;
using System.Text.Json;
using DiscordFloatingWidget.Models;

namespace DiscordFloatingWidget.Services;

public sealed class FavoritesService(string filePath)
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _canSave = true;
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DiscordFloatingWidget", "favorites.json");
    internal bool CanSave => _canSave;

    public async Task<(IReadOnlyList<FavoriteConversation> Favorites, string? Warning)> LoadAsync()
    {
        try
        {
            await using var stream = File.OpenRead(filePath);
            if (stream.Length > 256 * 1024) throw new InvalidDataException("The favorites file is too large.");
            var document = await JsonSerializer.DeserializeAsync<FavoritesDocument>(stream);
            if (document is not { Version: 1, Favorites: not null } || !Validate(document.Favorites, out var normalized))
                throw new InvalidDataException("The favorites file contains invalid or unsupported entries.");
            return (normalized, null);
        }
        catch (FileNotFoundException) { return ([], null); }
        catch (DirectoryNotFoundException) { return ([], null); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            _canSave = false;
            return ([], $"Favorites could not be loaded. The original file has been preserved and editing is disabled. " +
                $"Exit the app and rename '{filePath}' to reset favorites.\n\n{ex.Message}");
        }
    }

    public async Task<string?> SaveAsync(IReadOnlyList<FavoriteConversation> favorites)
    {
        if (!_canSave) return "Favorites editing is disabled because the existing file could not be loaded.";
        if (!Validate(favorites, out var normalized)) return "Use valid, uniquely named Discord favorites (up to 100).";
        await _writeLock.WaitAsync().ConfigureAwait(false);
        string? temporaryPath = null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
            temporaryPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(new FavoritesDocument(1, normalized),
                new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
            File.Move(temporaryPath, filePath, overwrite: true);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"Favorites could not be saved. Your previous list is unchanged.\n\n{ex.Message}";
        }
        finally
        {
            if (temporaryPath is not null)
            {
                try { File.Delete(temporaryPath); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
            _writeLock.Release();
        }
    }

    private static bool Validate(IReadOnlyList<FavoriteConversation> favorites, out List<FavoriteConversation> normalized)
    {
        normalized = [];
        if (favorites.Count > 100) return false;
        var ids = new HashSet<Guid>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in favorites)
        {
            if (item is null || !FavoriteConversation.TryCreate(item.Id, item.Name, item.Type, item.Url, out var valid, out _) ||
                !ids.Add(item.Id) || !names.Add(valid!.Name)) return false;
            normalized.Add(valid!);
        }
        return true;
    }

    private sealed record FavoritesDocument(int Version, List<FavoriteConversation>? Favorites);
}
