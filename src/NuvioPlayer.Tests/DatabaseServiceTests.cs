using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NuvioPlayer.Database;
using NuvioPlayer.Services;
using Xunit;

namespace NuvioPlayer.Tests;

public class DatabaseServiceTests
{
    private static DatabaseService CreateInMemoryDbService()
    {
        var options = new DbContextOptionsBuilder<NuvioDbContext>()
            .UseSqlite($"Data Source=file:memdb_{Guid.NewGuid():N}?mode=memory&cache=shared")
            .Options;

        var service = new DatabaseService(NullLogger<DatabaseService>.Instance, options);
        return service;
    }

    [Fact]
    public async Task Initialize_ShouldCreateTables()
    {
        var service = CreateInMemoryDbService();
        await service.InitializeAsync();

        var recent = await service.GetRecentMediaAsync();
        Assert.Empty(recent);
    }

    [Fact]
    public async Task RecordPlayback_ShouldAddAndIncrementPlayCount()
    {
        var service = CreateInMemoryDbService();
        await service.InitializeAsync();

        await service.RecordPlaybackAsync(@"C:\Videos\movie.mp4", "movie.mp4", 120000, 3600000);

        var history = await service.GetHistoryForFileAsync(@"C:\Videos\movie.mp4");
        Assert.NotNull(history);
        Assert.Equal("movie.mp4", history.DisplayName);
        Assert.Equal(120000, history.LastPositionMs);
        Assert.Equal(1, history.PlayCount);

        // Play again
        await service.RecordPlaybackAsync(@"C:\Videos\movie.mp4", "movie.mp4", 240000, 3600000);
        var updated = await service.GetHistoryForFileAsync(@"C:\Videos\movie.mp4");
        Assert.NotNull(updated);
        Assert.Equal(240000, updated.LastPositionMs);
        Assert.Equal(2, updated.PlayCount);
    }

    [Fact]
    public async Task Bookmarks_ShouldAddAndFetchByFile()
    {
        var service = CreateInMemoryDbService();
        await service.InitializeAsync();

        string file = @"C:\Videos\lecture.mp4";
        await service.AddBookmarkAsync(file, 45000, "Intro Section");
        await service.AddBookmarkAsync(file, 150000, "Q&A Section");

        var bookmarks = await service.GetBookmarksForFileAsync(file);
        Assert.Equal(2, bookmarks.Count);
        Assert.Equal("Intro Section", bookmarks[0].Label);
        Assert.Equal("Q&A Section", bookmarks[1].Label);

        await service.RemoveBookmarkAsync(bookmarks[0].Id);
        var remaining = await service.GetBookmarksForFileAsync(file);
        Assert.Single(remaining);
        Assert.Equal("Q&A Section", remaining[0].Label);
    }

    [Fact]
    public async Task Playlists_ShouldSaveAndRetrieve()
    {
        var service = CreateInMemoryDbService();
        await service.InitializeAsync();

        var files = new[] { @"C:\Videos\1.mp4", @"C:\Videos\2.mp4", @"C:\Videos\3.mp4" };
        var saved = await service.SavePlaylistAsync("Favorites", files);

        Assert.Equal("Favorites", saved.Name);
        Assert.Equal(3, saved.Items.Count);

        var all = await service.GetAllPlaylistsAsync();
        Assert.Single(all);
        Assert.Equal(3, all[0].Items.Count);

        await service.DeletePlaylistAsync(saved.Id);
        var afterDelete = await service.GetAllPlaylistsAsync();
        Assert.Empty(afterDelete);
    }

    [Fact]
    public async Task ActivePlaylist_ShouldSaveAndLoad()
    {
        var service = CreateInMemoryDbService();
        await service.InitializeAsync();

        var activeFiles = new[] { @"C:\v1.mp4", @"C:\v2.mp4" };
        await service.SaveActivePlaylistAsync(activeFiles);

        var loaded = await service.LoadActivePlaylistAsync();
        Assert.Equal(2, loaded.Count);
        Assert.Equal(@"C:\v1.mp4", loaded[0]);
        Assert.Equal(@"C:\v2.mp4", loaded[1]);
    }
}
