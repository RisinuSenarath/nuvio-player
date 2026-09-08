using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NuvioPlayer.Database;
using NuvioPlayer.Services;
using NuvioPlayer.ViewModels;
using Xunit;

namespace NuvioPlayer.Tests;

public class ResumePlaybackTests
{
    private (MainViewModel vm, MockMediaPlayerService mockMedia, DatabaseService db) CreateTestContext()
    {
        var settingsService = new SettingsService(NullLogger<SettingsService>.Instance);
        var mockMedia = new MockMediaPlayerService();
        var playlistService = new PlaylistService(NullLogger<PlaylistService>.Instance);
        var playlistViewModel = new PlaylistViewModel(playlistService, mockMedia);

        var dbOptions = new DbContextOptionsBuilder<NuvioDbContext>()
            .UseSqlite($"Data Source=file:memdb_resume_{Guid.NewGuid():N}?mode=memory&cache=shared")
            .Options;
        var db = new DatabaseService(NullLogger<DatabaseService>.Instance, dbOptions);
        _ = db.InitializeAsync();

        var vm = new MainViewModel(
            NullLogger<MainViewModel>.Instance,
            settingsService,
            mockMedia,
            playlistService,
            playlistViewModel,
            db);

        return (vm, mockMedia, db);
    }

    [Fact]
    public async Task ResumePrompt_ShouldAppear_WhenEligiblePosition()
    {
        var (vm, mockMedia, db) = CreateTestContext();
        await db.InitializeAsync();

        string file = @"C:\Videos\test_resume.mp4";
        // 45 seconds into a 3 minute video
        await db.RecordPlaybackAsync(file, "test_resume.mp4", 45000, 180000);

        await vm.OpenMediaFileAsync(file);

        Assert.True(vm.IsResumePromptVisible);
        Assert.Equal("Resume from 00:45?", vm.ResumePromptText);
    }

    [Fact]
    public async Task ResumePrompt_ShouldNotAppear_WhenNearBeginning()
    {
        var (vm, mockMedia, db) = CreateTestContext();
        await db.InitializeAsync();

        string file = @"C:\Videos\test_start.mp4";
        // 5 seconds into video (< 10s threshold)
        await db.RecordPlaybackAsync(file, "test_start.mp4", 5000, 180000);

        await vm.OpenMediaFileAsync(file);

        Assert.False(vm.IsResumePromptVisible);
    }

    [Fact]
    public async Task ResumePrompt_ShouldNotAppear_WhenNearEnd()
    {
        var (vm, mockMedia, db) = CreateTestContext();
        await db.InitializeAsync();

        string file = @"C:\Videos\test_end.mp4";
        // 175 seconds into 180s video (within last 15s)
        await db.RecordPlaybackAsync(file, "test_end.mp4", 175000, 180000);

        await vm.OpenMediaFileAsync(file);

        Assert.False(vm.IsResumePromptVisible);
    }

    [Fact]
    public async Task ConfirmResume_ShouldSeekAndHidePrompt()
    {
        var (vm, mockMedia, db) = CreateTestContext();
        await db.InitializeAsync();

        string file = @"C:\Videos\test_confirm.mp4";
        await db.RecordPlaybackAsync(file, "test_confirm.mp4", 50000, 180000);

        await vm.OpenMediaFileAsync(file);
        Assert.True(vm.IsResumePromptVisible);

        vm.ResumePlaybackCommand.Execute(null);

        Assert.False(vm.IsResumePromptVisible);
        Assert.Equal(TimeSpan.FromMilliseconds(50000), mockMedia.Position);
    }

    [Fact]
    public async Task StartOver_ShouldSeekToZeroAndHidePrompt()
    {
        var (vm, mockMedia, db) = CreateTestContext();
        await db.InitializeAsync();

        string file = @"C:\Videos\test_startover.mp4";
        await db.RecordPlaybackAsync(file, "test_startover.mp4", 50000, 180000);

        await vm.OpenMediaFileAsync(file);
        Assert.True(vm.IsResumePromptVisible);

        vm.StartOverPlaybackCommand.Execute(null);

        Assert.False(vm.IsResumePromptVisible);
        Assert.Equal(TimeSpan.Zero, mockMedia.Position);
    }
}
