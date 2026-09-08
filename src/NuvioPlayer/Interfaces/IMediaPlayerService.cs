using NuvioPlayer.Models;

namespace NuvioPlayer.Interfaces;

public interface IMediaPlayerService : IDisposable
{
    bool IsPlaying { get; }
    bool IsPaused { get; }
    bool IsSeekable { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    float PositionFraction { get; }
    int Volume { get; set; }
    bool IsMuted { get; set; }
    double PlaybackRate { get; set; }
    string? CurrentMediaPath { get; }
    string? CurrentMediaTitle { get; }
    PlaybackState State { get; }
    MediaTrackInfo CurrentTrackInfo { get; }
    object? NativePlayer { get; }

    event EventHandler<PlaybackState>? StateChanged;
    event EventHandler<TimeSpan>? PositionChanged;
    event EventHandler<TimeSpan>? DurationChanged;
    event EventHandler<string>? MediaOpened;
    event EventHandler? MediaEnded;
    event EventHandler<string>? ErrorOccurred;
    event EventHandler<MediaTrackInfo>? TracksUpdated;

    Task<bool> OpenMediaAsync(string filePathOrUri);
    void Play();
    void Pause();
    void TogglePlayPause();
    void Stop();
    void SeekTo(TimeSpan position);
    void SeekToFraction(float fraction);
    void SeekRelative(TimeSpan offset);
    bool TakeSnapshot(string outputPath, uint width = 0, uint height = 0);
    void SetSubtitleTrack(int trackId);
    void SetAudioTrack(int trackId);
    void SetVideoTrack(int trackId);
    bool AddSubtitleFile(string subtitlePath);
    void SetSubtitleDelay(long delayMicroseconds);
    void SetAudioDelay(long delayMicroseconds);
    void SetAspectRatio(string? aspectRatio);
    MediaDetails? GetCurrentMediaDetails();
}
