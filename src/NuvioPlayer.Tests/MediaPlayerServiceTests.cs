using NuvioPlayer.Models;
using Xunit;

namespace NuvioPlayer.Tests;

public class MediaPlayerServiceTests
{
    [Fact]
    public void MediaTrackInfo_DefaultState_ShouldBeEmptyAndUnselected()
    {
        var info = new MediaTrackInfo();

        Assert.Empty(info.AudioTracks);
        Assert.Empty(info.SubtitleTracks);
        Assert.Empty(info.VideoTracks);
        Assert.Equal(-1, info.SelectedAudioTrackId);
        Assert.Equal(-1, info.SelectedSubtitleTrackId);
        Assert.Equal(-1, info.SelectedVideoTrackId);
    }

    [Fact]
    public void MediaDetails_ShouldHoldCorrectProperties()
    {
        var details = new MediaDetails
        {
            FileName = "sample.mp4",
            FilePath = @"C:\Videos\sample.mp4",
            Container = "MP4",
            Duration = TimeSpan.FromMinutes(2),
            Width = 1920,
            Height = 1080,
            VideoCodec = "H264",
            FrameRate = 29.97f,
            AudioCodec = "AAC",
            AudioChannels = 2,
            AudioSampleRate = 48000
        };

        Assert.Equal("sample.mp4", details.FileName);
        Assert.Equal("MP4", details.Container);
        Assert.Equal(1920u, details.Width);
        Assert.Equal(1080u, details.Height);
        Assert.Equal(120, details.Duration.TotalSeconds);
    }

    [Fact]
    public void PlaybackState_AllEnumStates_ShouldBeDefined()
    {
        var states = Enum.GetValues<PlaybackState>();

        Assert.Contains(PlaybackState.Stopped, states);
        Assert.Contains(PlaybackState.Opening, states);
        Assert.Contains(PlaybackState.Playing, states);
        Assert.Contains(PlaybackState.Paused, states);
        Assert.Contains(PlaybackState.Ended, states);
        Assert.Contains(PlaybackState.Error, states);
    }

    [Fact]
    public void LibVlc_Initialization_ShouldSucceed()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string arch = Environment.Is64BitProcess ? "win-x64" : "win-x86";
        string libVlcPath = System.IO.Path.Combine(baseDir, "libvlc", arch);

        if (System.IO.Directory.Exists(libVlcPath))
        {
            LibVLCSharp.Shared.Core.Initialize(libVlcPath);
        }
        else
        {
            LibVLCSharp.Shared.Core.Initialize();
        }

        using var libVlc = new LibVLCSharp.Shared.LibVLC("--no-video-title-show");
        using var player = new LibVLCSharp.Shared.MediaPlayer(libVlc);

        Assert.NotNull(libVlc);
        Assert.NotNull(player);
        Assert.False(player.IsPlaying);
    }
}
