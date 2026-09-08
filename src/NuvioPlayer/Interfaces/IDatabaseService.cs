using NuvioPlayer.Database.Entities;

namespace NuvioPlayer.Interfaces;

public interface IDatabaseService
{
    Task InitializeAsync();

    // History
    Task RecordPlaybackAsync(string filePath, string displayName, long positionMs, long durationMs);
    Task<List<MediaHistoryEntity>> GetRecentMediaAsync(int count = 20);
    Task<MediaHistoryEntity?> GetHistoryForFileAsync(string filePath);
    Task UpdatePositionAsync(string filePath, long positionMs);
    Task RemoveHistoryItemAsync(int id);
    Task ClearHistoryAsync();

    // Bookmarks
    Task<BookmarkEntity> AddBookmarkAsync(string filePath, long positionMs, string label);
    Task<List<BookmarkEntity>> GetBookmarksForFileAsync(string filePath);
    Task RemoveBookmarkAsync(int id);
    Task ClearBookmarksAsync();

    // Playlists
    Task<List<PlaylistEntity>> GetAllPlaylistsAsync();
    Task<PlaylistEntity> SavePlaylistAsync(string name, IEnumerable<string> filePaths);
    Task DeletePlaylistAsync(int id);
    Task ClearPlaylistsAsync();
    Task SaveActivePlaylistAsync(IEnumerable<string> filePaths);
    Task<List<string>> LoadActivePlaylistAsync();
}
