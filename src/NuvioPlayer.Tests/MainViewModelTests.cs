using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NuvioPlayer.Interfaces;
using NuvioPlayer.Models;
using NuvioPlayer.Services;
using NuvioPlayer.ViewModels;
using Xunit;

namespace NuvioPlayer.Tests;

#pragma warning disable CS0067
// Lightweight mock for IMediaPlayerService
internal class MockMediaPlayerService : IMediaPlayerService
{
    public bool IsPlaying { get; set; } = false;
    public bool IsPaused { get; set; } = false;
    public bool IsSeekable { get; set; } = true;
    public TimeSpan Position { get; set; } = TimeSpan.Zero;
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(5);
    public float PositionFraction => (float)(Position.TotalSeconds / Duration.TotalSeconds);
    public int Volume { get; set; } = 100;
    public bool IsMuted { get; set; } = false;
    public double PlaybackRate { get; set; } = 1.0;
    public string? CurrentMediaPath { get; set; }
    public string? CurrentMediaTitle { get; set; }
    public PlaybackState State { get; set; } = PlaybackState.Stopped;
    public MediaTrackInfo CurrentTrackInfo { get; set; } = new();
    public object? NativePlayer => null;

    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler<TimeSpan>? DurationChanged;
    public event EventHandler<string>? MediaOpened;
    public event EventHandler? MediaEnded;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<MediaTrackInfo>? TracksUpdated;

    public Task<bool> OpenMediaAsync(string filePathOrUri)
    {
        CurrentMediaPath = filePathOrUri;
        CurrentMediaTitle = System.IO.Path.GetFileName(filePathOrUri);
        State = PlaybackState.Playing;
        IsPlaying = true;
        StateChanged?.Invoke(this, State);
        MediaOpened?.Invoke(this, filePathOrUri);
        return Task.FromResult(true);
    }

    public void Play() { IsPlaying = true; IsPaused = false; State = PlaybackState.Playing; StateChanged?.Invoke(this, State); }
    public void Pause() { IsPlaying = false; IsPaused = true; State = PlaybackState.Paused; StateChanged?.Invoke(this, State); }
    public void TogglePlayPause()
    {
        if (IsPlaying) Pause();
        else Play();
    }
    public void Stop() { IsPlaying = false; IsPaused = false; State = PlaybackState.Stopped; StateChanged?.Invoke(this, State); }
    public void SeekTo(TimeSpan position) { Position = position; PositionChanged?.Invoke(this, Position); }
    public void SeekToFraction(float fraction) { Position = TimeSpan.FromSeconds(Duration.TotalSeconds * fraction); }
    public void SeekRelative(TimeSpan offset) { Position += offset; PositionChanged?.Invoke(this, Position); }
    public bool TakeSnapshot(string outputPath, uint width = 0, uint height = 0) => true;
    public int LastSubtitleTrackSet { get; set; } = -2;
    public int LastAudioTrackSet { get; set; } = -2;
    public int LastVideoTrackSet { get; set; } = -2;
    public long LastSubtitleDelaySet { get; set; } = 0;
    public long LastAudioDelaySet { get; set; } = 0;
    public string? LastSubtitleFileAdded { get; set; }

    public void SetSubtitleTrack(int trackId) { LastSubtitleTrackSet = trackId; }
    public void SetAudioTrack(int trackId) { LastAudioTrackSet = trackId; }
    public void SetVideoTrack(int trackId) { LastVideoTrackSet = trackId; }
    public bool AddSubtitleFile(string subtitlePath) { LastSubtitleFileAdded = subtitlePath; return true; }
    public void SetSubtitleDelay(long delayMicroseconds) { LastSubtitleDelaySet = delayMicroseconds; }
    public void SetAudioDelay(long delayMicroseconds) { LastAudioDelaySet = delayMicroseconds; }
    public void SetAspectRatio(string? aspectRatio) { }
    public MediaDetails? GetCurrentMediaDetails() => new() { FileName = CurrentMediaTitle ?? "Test" };
    public void Dispose() { }
}

public class MainViewModelTests
{
    private readonly ISettingsService _settingsService;
    private readonly MockMediaPlayerService _mockMediaService;
    private readonly IPlaylistService _playlistService;
    private readonly PlaylistViewModel _playlistViewModel;
    private readonly IDatabaseService _databaseService;
    private readonly MainViewModel _viewModel;

    public MainViewModelTests()
    {
        _settingsService = new SettingsService(NullLogger<SettingsService>.Instance);
        _mockMediaService = new MockMediaPlayerService();
        _playlistService = new PlaylistService(NullLogger<PlaylistService>.Instance);
        _playlistViewModel = new PlaylistViewModel(_playlistService, _mockMediaService);

        var dbOptions = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<NuvioPlayer.Database.NuvioDbContext>()
            .UseSqlite($"Data Source=file:memdb_mvm_{Guid.NewGuid():N}?mode=memory&cache=shared")
            .Options;
        _databaseService = new DatabaseService(NullLogger<DatabaseService>.Instance, dbOptions);
        _ = _databaseService.InitializeAsync();

        _viewModel = new MainViewModel(
            NullLogger<MainViewModel>.Instance,
            _settingsService,
            _mockMediaService,
            _playlistService,
            _playlistViewModel,
            _databaseService);
    }

    [Fact]
    public void ToggleFullscreen_ShouldInvertState()
    {
        Assert.False(_viewModel.IsFullscreen);

        _viewModel.ToggleFullscreenCommand.Execute(null);
        Assert.True(_viewModel.IsFullscreen);

        _viewModel.ToggleFullscreenCommand.Execute(null);
        Assert.False(_viewModel.IsFullscreen);
    }

    [Fact]
    public void TogglePlaylist_ShouldInvertState()
    {
        Assert.False(_viewModel.IsPlaylistOpen);

        _viewModel.TogglePlaylistCommand.Execute(null);
        Assert.True(_viewModel.IsPlaylistOpen);

        _viewModel.TogglePlaylistCommand.Execute(null);
        Assert.False(_viewModel.IsPlaylistOpen);
    }

    [Fact]
    public void ChangeVolume_ShouldUpdateVolumeAndClamp()
    {
        _viewModel.Volume = 95;
        _viewModel.ChangeVolumeCommand.Execute(10);
        Assert.Equal(105, _viewModel.Volume);

        _viewModel.ChangeVolumeCommand.Execute(60);
        Assert.Equal(150, _viewModel.Volume);

        _viewModel.ChangeVolumeCommand.Execute(-200);
        Assert.Equal(0, _viewModel.Volume);
    }

    [Fact]
    public void ChangeVolume_WhenOver100_ShouldShowBoostOsd()
    {
        _viewModel.Volume = 110;
        _viewModel.ChangeVolumeCommand.Execute(5);
        Assert.Equal(115, _viewModel.Volume);
        Assert.Contains("Boost", _viewModel.OsdMessage);
    }

    [Fact]
    public void ToggleMute_ShouldToggleMuteFlag()
    {
        _viewModel.IsMuted = false;
        _viewModel.ToggleMuteCommand.Execute(null);
        Assert.True(_viewModel.IsMuted);

        _viewModel.ToggleMuteCommand.Execute(null);
        Assert.False(_viewModel.IsMuted);
    }

    [Fact]
    public void SetPlaybackRate_ShouldAcceptStringOrDouble()
    {
        _viewModel.SetPlaybackRateCommand.Execute("1.5");
        Assert.Equal(1.5, _viewModel.PlaybackRate);
        Assert.Equal("1.5x", _viewModel.PlaybackRateText);

        _viewModel.SetPlaybackRateCommand.Execute(2.0);
        Assert.Equal(2.0, _viewModel.PlaybackRate);
        Assert.Equal("2.0x", _viewModel.PlaybackRateText);
    }

    [Fact]
    public void CyclePlaybackRate_ShouldProgressThroughSpeeds()
    {
        _viewModel.PlaybackRate = 1.0;
        _viewModel.CyclePlaybackRateCommand.Execute(null);
        Assert.Equal(1.25, _viewModel.PlaybackRate);
    }

    [Fact]
    public async Task OpenMedia_ShouldUpdateStateAndHasMedia()
    {
        await _viewModel.OpenMediaFileAsync(@"C:\Videos\test.mp4");

        Assert.True(_viewModel.HasMedia);
        Assert.Equal("test.mp4", _viewModel.MediaTitle);
        Assert.True(_viewModel.IsPlaying);
    }

    [Fact]
    public void SelectSubtitleTrack_ShouldUpdatePropertyAndCallService()
    {
        _viewModel.SubtitleTracks.Add(new TrackItem { Id = 1, Name = "English (SRT)" });
        _viewModel.SubtitleTracks.Add(new TrackItem { Id = 2, Name = "Spanish (SRT)" });

        _viewModel.SelectSubtitleTrackCommand.Execute(1);

        Assert.Equal(1, _viewModel.SelectedSubtitleTrackId);
        Assert.Equal(1, _mockMediaService.LastSubtitleTrackSet);
        Assert.Equal("English (SRT)", _viewModel.CurrentSubtitleTrackName);

        // Disable
        _viewModel.SelectSubtitleTrackCommand.Execute(-1);
        Assert.Equal(-1, _viewModel.SelectedSubtitleTrackId);
        Assert.Equal(-1, _mockMediaService.LastSubtitleTrackSet);
        Assert.Equal("Disabled", _viewModel.CurrentSubtitleTrackName);
    }

    [Fact]
    public void ToggleSubtitles_ShouldToggleBetweenFirstTrackAndDisabled()
    {
        _viewModel.SubtitleTracks.Add(new TrackItem { Id = 3, Name = "English" });

        _viewModel.SelectedSubtitleTrackId = -1;
        _viewModel.ToggleSubtitlesCommand.Execute(null);

        Assert.Equal(3, _viewModel.SelectedSubtitleTrackId);
        Assert.Equal(3, _mockMediaService.LastSubtitleTrackSet);

        // Toggle again to disable
        _viewModel.ToggleSubtitlesCommand.Execute(null);
        Assert.Equal(-1, _viewModel.SelectedSubtitleTrackId);
        Assert.Equal(-1, _mockMediaService.LastSubtitleTrackSet);
    }

    [Fact]
    public void SelectAudioTrack_ShouldUpdatePropertyAndCallService()
    {
        _viewModel.AudioTracks.Add(new TrackItem { Id = 1, Name = "English 5.1" });
        _viewModel.AudioTracks.Add(new TrackItem { Id = 2, Name = "Director Commentary" });

        _viewModel.SelectAudioTrackCommand.Execute(2);

        Assert.Equal(2, _viewModel.SelectedAudioTrackId);
        Assert.Equal(2, _mockMediaService.LastAudioTrackSet);
        Assert.Equal("Director Commentary", _viewModel.CurrentAudioTrackName);
    }

    [Fact]
    public void AdjustSubtitleDelay_ShouldIncrementAndSetService()
    {
        _viewModel.AdjustSubtitleDelayCommand.Execute(50);
        // 50ms = 50,000 microseconds
        Assert.Equal(50000, _mockMediaService.LastSubtitleDelaySet);

        _viewModel.AdjustSubtitleDelayCommand.Execute(-100);
        // 50 - 100 = -50ms = -50,000 microseconds
        Assert.Equal(-50000, _mockMediaService.LastSubtitleDelaySet);

        _viewModel.ResetSubtitleDelayCommand.Execute(null);
        Assert.Equal(0, _mockMediaService.LastSubtitleDelaySet);
    }

    [Fact]
    public void AdjustAudioDelay_ShouldIncrementAndSetService()
    {
        _viewModel.AdjustAudioDelayCommand.Execute(100);
        Assert.Equal(100000, _mockMediaService.LastAudioDelaySet);

        _viewModel.ResetAudioDelayCommand.Execute(null);
        Assert.Equal(0, _mockMediaService.LastAudioDelaySet);
    }

    [Fact]
    public async Task Bookmarks_AddJumpAndRemove_ShouldWorkEndToEnd()
    {
        await _databaseService.InitializeAsync();
        string testFile = @"C:\Videos\test_bookmark.mp4";
        await _viewModel.OpenMediaFileAsync(testFile);

        _mockMediaService.SeekTo(TimeSpan.FromSeconds(45));

        await _viewModel.AddBookmarkAsync("Interesting scene");

        Assert.Single(_viewModel.Bookmarks);
        var bm = _viewModel.Bookmarks[0];
        Assert.Equal("Interesting scene", bm.Label);
        Assert.Equal(45000, bm.PositionMs);
        Assert.Equal("00:45", bm.FormattedPosition);

        // Jump to bookmark
        _mockMediaService.SeekTo(TimeSpan.Zero);
        _viewModel.JumpToBookmarkCommand.Execute(bm);
        Assert.Equal(TimeSpan.FromMilliseconds(45000), _mockMediaService.Position);

        // Remove bookmark
        await _viewModel.RemoveBookmarkAsync(bm);
        Assert.Empty(_viewModel.Bookmarks);
    }

    [Fact]
    public void ToggleBookmarks_ShouldCoordinateWithSidebar()
    {
        Assert.False(_viewModel.IsBookmarksOpen);
        Assert.False(_viewModel.IsRightSidebarOpen);

        _viewModel.ToggleBookmarksCommand.Execute(null);
        Assert.True(_viewModel.IsBookmarksOpen);
        Assert.False(_viewModel.IsPlaylistOpen);
        Assert.True(_viewModel.IsRightSidebarOpen);

        _viewModel.ShowPlaylistTabCommand.Execute(null);
        Assert.False(_viewModel.IsBookmarksOpen);
        Assert.True(_viewModel.IsPlaylistOpen);
        Assert.True(_viewModel.IsRightSidebarOpen);

        _viewModel.CloseSidebarCommand.Execute(null);
        Assert.False(_viewModel.IsBookmarksOpen);
        Assert.False(_viewModel.IsPlaylistOpen);
        Assert.False(_viewModel.IsRightSidebarOpen);
    }

    [Fact]
    public async Task RecentMedia_ShouldPopulateAndAllowClear()
    {
        await _databaseService.InitializeAsync();

        string file1 = @"C:\Videos\movie1.mp4";
        string file2 = @"C:\Videos\movie2.mp4";

        await _databaseService.RecordPlaybackAsync(file1, "movie1.mp4", 30000, 120000);
        await _databaseService.RecordPlaybackAsync(file2, "movie2.mp4", 60000, 180000);

        await _viewModel.LoadRecentMediaAsync();

        Assert.Equal(2, _viewModel.RecentMedia.Count);
        Assert.True(_viewModel.HasRecentMedia);

        var first = _viewModel.RecentMedia[0];
        await _viewModel.RemoveRecentMediaCommand.ExecuteAsync(first);

        Assert.Single(_viewModel.RecentMedia);

        await _viewModel.ClearRecentMediaCommand.ExecuteAsync(null);
        Assert.Empty(_viewModel.RecentMedia);
        Assert.False(_viewModel.HasRecentMedia);
    }

    [Fact]
    public void MediaDetails_ShouldFormatResolutionAndSize()
    {
        var details = new NuvioPlayer.Models.MediaDetails
        {
            FileName = "avatar.mkv",
            FilePath = @"C:\Videos\avatar.mkv",
            Container = "MKV",
            FileSizeBytes = 1024 * 1024 * 750, // 750 MB
            Duration = TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(15)),
            Width = 1920,
            Height = 1080,
            VideoCodec = "H264",
            FrameRate = 23.976f,
            AudioCodec = "AC3",
            AudioChannels = 6,
            AudioSampleRate = 48000
        };

        Assert.Equal("1920 × 1080", details.ResolutionText);
        Assert.Equal("750 MB", details.FormattedFileSize);
        Assert.Equal("2:15:00", details.FormattedDuration);
        Assert.Equal("5.1 Surround", details.AudioChannelsText);
        Assert.Equal("23.98 fps", details.FrameRateText);
    }
}
