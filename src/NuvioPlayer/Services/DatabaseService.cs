using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NuvioPlayer.Database;
using NuvioPlayer.Database.Entities;
using NuvioPlayer.Interfaces;

namespace NuvioPlayer.Services;

public class DatabaseService : IDatabaseService
{
    private readonly ILogger<DatabaseService> _logger;
    private readonly string _dbPath;
    private readonly DbContextOptions<NuvioDbContext> _options;

    public DatabaseService(ILogger<DatabaseService> logger, ISettingsService settingsService)
    {
        _logger = logger;
        string appDataDir = settingsService.GetAppDataPath();
        Directory.CreateDirectory(appDataDir);
        _dbPath = Path.Combine(appDataDir, "nuvio.db");

        var builder = new DbContextOptionsBuilder<NuvioDbContext>();
        builder.UseSqlite($"Data Source={_dbPath}");
        _options = builder.Options;
    }

    // Secondary constructor for testing with in-memory or custom options
    public DatabaseService(ILogger<DatabaseService> logger, DbContextOptions<NuvioDbContext> options)
    {
        _logger = logger;
        _dbPath = ":memory:";
        _options = options;
    }

    private NuvioDbContext CreateContext() => new NuvioDbContext(_options);

    public async Task InitializeAsync()
    {
        try
        {
            await using var context = CreateContext();
            await context.Database.EnsureCreatedAsync();
            _logger.LogInformation("Database initialized successfully at {Path}", _dbPath);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to initialize SQLite database at {Path}", _dbPath);
            throw;
        }
    }

    public async Task RecordPlaybackAsync(string filePath, string displayName, long positionMs, long durationMs)
    {
        try
        {
            await using var context = CreateContext();
            var existing = await context.MediaHistories.FirstOrDefaultAsync(h => h.FilePath == filePath);

            if (existing != null)
            {
                existing.DisplayName = displayName;
                if (positionMs > 0 || existing.LastPositionMs == 0)
                {
                    existing.LastPositionMs = positionMs;
                }
                if (durationMs > 0 && existing.DurationMs == 0)
                {
                    existing.DurationMs = durationMs;
                }
                existing.LastPlayedAt = DateTime.UtcNow;
                existing.PlayCount++;
            }
            else
            {
                context.MediaHistories.Add(new MediaHistoryEntity
                {
                    FilePath = filePath,
                    DisplayName = displayName,
                    LastPositionMs = positionMs,
                    DurationMs = durationMs,
                    LastPlayedAt = DateTime.UtcNow,
                    PlayCount = 1
                });
            }

            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording playback for {Path}", filePath);
        }
    }

    public async Task<List<MediaHistoryEntity>> GetRecentMediaAsync(int count = 20)
    {
        try
        {
            await using var context = CreateContext();
            return await context.MediaHistories
                .OrderByDescending(h => h.LastPlayedAt)
                .Take(count)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent media history");
            return new List<MediaHistoryEntity>();
        }
    }

    public async Task<MediaHistoryEntity?> GetHistoryForFileAsync(string filePath)
    {
        try
        {
            await using var context = CreateContext();
            return await context.MediaHistories.FirstOrDefaultAsync(h => h.FilePath == filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving history for {Path}", filePath);
            return null;
        }
    }

    public async Task UpdatePositionAsync(string filePath, long positionMs)
    {
        try
        {
            await using var context = CreateContext();
            var existing = await context.MediaHistories.FirstOrDefaultAsync(h => h.FilePath == filePath);
            if (existing != null)
            {
                existing.LastPositionMs = positionMs;
                existing.LastPlayedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating position for {Path}", filePath);
        }
    }

    public async Task RemoveHistoryItemAsync(int id)
    {
        try
        {
            await using var context = CreateContext();
            var item = await context.MediaHistories.FindAsync(id);
            if (item != null)
            {
                context.MediaHistories.Remove(item);
                await context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing history item {Id}", id);
        }
    }

    public async Task ClearHistoryAsync()
    {
        try
        {
            await using var context = CreateContext();
            context.MediaHistories.RemoveRange(context.MediaHistories);
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing media history");
        }
    }

    public async Task<BookmarkEntity> AddBookmarkAsync(string filePath, long positionMs, string label)
    {
        try
        {
            await using var context = CreateContext();
            var bookmark = new BookmarkEntity
            {
                FilePath = filePath,
                PositionMs = positionMs,
                Label = label,
                CreatedAt = DateTime.UtcNow
            };

            context.Bookmarks.Add(bookmark);
            await context.SaveChangesAsync();
            return bookmark;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding bookmark for {Path}", filePath);
            throw;
        }
    }

    public async Task<List<BookmarkEntity>> GetBookmarksForFileAsync(string filePath)
    {
        try
        {
            await using var context = CreateContext();
            return await context.Bookmarks
                .Where(b => b.FilePath == filePath)
                .OrderBy(b => b.PositionMs)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching bookmarks for {Path}", filePath);
            return new List<BookmarkEntity>();
        }
    }

    public async Task RemoveBookmarkAsync(int id)
    {
        try
        {
            await using var context = CreateContext();
            var item = await context.Bookmarks.FindAsync(id);
            if (item != null)
            {
                context.Bookmarks.Remove(item);
                await context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing bookmark {Id}", id);
        }
    }

    public async Task ClearBookmarksAsync()
    {
        try
        {
            await using var context = CreateContext();
            context.Bookmarks.RemoveRange(context.Bookmarks);
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing bookmarks");
        }
    }

    public async Task<List<PlaylistEntity>> GetAllPlaylistsAsync()
    {
        try
        {
            await using var context = CreateContext();
            return await context.Playlists
                .Include(p => p.Items)
                .OrderByDescending(p => p.UpdatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching playlists");
            return new List<PlaylistEntity>();
        }
    }

    public async Task<PlaylistEntity> SavePlaylistAsync(string name, IEnumerable<string> filePaths)
    {
        try
        {
            await using var context = CreateContext();
            var existing = await context.Playlists
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Name == name);

            if (existing != null)
            {
                existing.UpdatedAt = DateTime.UtcNow;
                existing.Items.Clear();

                int order = 0;
                foreach (var path in filePaths)
                {
                    existing.Items.Add(new PlaylistItemEntity
                    {
                        FilePath = path,
                        DisplayName = Path.GetFileName(path),
                        SortOrder = order++
                    });
                }
                await context.SaveChangesAsync();
                return existing;
            }

            var playlist = new PlaylistEntity
            {
                Name = name,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            int sort = 0;
            foreach (var path in filePaths)
            {
                playlist.Items.Add(new PlaylistItemEntity
                {
                    FilePath = path,
                    DisplayName = Path.GetFileName(path),
                    SortOrder = sort++
                });
            }

            context.Playlists.Add(playlist);
            await context.SaveChangesAsync();
            return playlist;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving playlist {Name}", name);
            throw;
        }
    }

    public async Task DeletePlaylistAsync(int id)
    {
        try
        {
            await using var context = CreateContext();
            var playlist = await context.Playlists.FindAsync(id);
            if (playlist != null)
            {
                context.Playlists.Remove(playlist);
                await context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting playlist {Id}", id);
        }
    }

    public async Task ClearPlaylistsAsync()
    {
        try
        {
            await using var context = CreateContext();
            context.Playlists.RemoveRange(context.Playlists);
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing playlists");
        }
    }

    public async Task SaveActivePlaylistAsync(IEnumerable<string> filePaths)
    {
        await SavePlaylistAsync("__DefaultActivePlaylist__", filePaths);
    }

    public async Task<List<string>> LoadActivePlaylistAsync()
    {
        try
        {
            await using var context = CreateContext();
            var playlist = await context.Playlists
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Name == "__DefaultActivePlaylist__");

            if (playlist != null)
            {
                return playlist.Items
                    .OrderBy(i => i.SortOrder)
                    .Select(i => i.FilePath)
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading active playlist");
        }

        return new List<string>();
    }
}
