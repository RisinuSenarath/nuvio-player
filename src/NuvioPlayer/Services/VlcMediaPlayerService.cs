using System.IO;
using System.Windows;
using LibVLCSharp.Shared;
using Microsoft.Extensions.Logging;
using NuvioPlayer.Interfaces;
using NuvioPlayer.Models;

namespace NuvioPlayer.Services;

public class VlcMediaPlayerService : IMediaPlayerService
{
    private readonly ILogger<VlcMediaPlayerService> _logger;
    private readonly ISettingsService _settingsService;
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private Media? _currentMedia;
    private bool _isDisposed;

    public bool IsPlaying => _mediaPlayer?.IsPlaying ?? false;
    public bool IsPaused => State == PlaybackState.Paused;
    public bool IsSeekable => _mediaPlayer?.IsSeekable ?? false;

    public TimeSpan Position
    {
        get
        {
            long time = _mediaPlayer?.Time ?? 0;
            return time > 0 ? TimeSpan.FromMilliseconds(time) : TimeSpan.Zero;
        }
    }

    public TimeSpan Duration
    {
        get
        {
            long length = _mediaPlayer?.Length ?? 0;
            return length > 0 ? TimeSpan.FromMilliseconds(length) : TimeSpan.Zero;
        }
    }

    public float PositionFraction => _mediaPlayer?.Position ?? 0f;

    public int Volume
    {
        get => _mediaPlayer?.Volume ?? 0;
        set
        {
            if (_mediaPlayer != null)
            {
                int clamped = Math.Clamp(value, 0, 150);
                _mediaPlayer.Volume = clamped;
                _settingsService.Settings.Volume = clamped;
            }
        }
    }

    public bool IsMuted
    {
        get => _mediaPlayer?.Mute ?? false;
        set
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Mute = value;
                _settingsService.Settings.IsMuted = value;
            }
        }
    }

    public double PlaybackRate
    {
        get => _mediaPlayer?.Rate ?? 1.0f;
        set
        {
            if (_mediaPlayer != null && value > 0.1 && value <= 4.0)
            {
                _mediaPlayer.SetRate((float)value);
            }
        }
    }

    public string? CurrentMediaPath { get; private set; }
    public string? CurrentMediaTitle { get; private set; }
    public PlaybackState State { get; private set; } = PlaybackState.Stopped;
    public MediaTrackInfo CurrentTrackInfo { get; private set; } = new();
    public object? NativePlayer => _mediaPlayer;

    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler<TimeSpan>? DurationChanged;
    public event EventHandler<string>? MediaOpened;
    public event EventHandler? MediaEnded;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<MediaTrackInfo>? TracksUpdated;

    public VlcMediaPlayerService(ILogger<VlcMediaPlayerService> logger, ISettingsService settingsService)
    {
        _logger = logger;
        _settingsService = settingsService;
        InitializeLibVlc();
    }

    private void InitializeLibVlc()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string arch = Environment.Is64BitProcess ? "win-x64" : "win-x86";
            string libVlcPath = Path.Combine(baseDir, "libvlc", arch);

            if (Directory.Exists(libVlcPath))
            {
                _logger.LogInformation("Initializing LibVLC runtime from bundled path: {Path}", libVlcPath);
                Core.Initialize(libVlcPath);
            }
            else
            {
                _logger.LogWarning("Bundled LibVLC directory not found at {Path}, trying default Core.Initialize()", libVlcPath);
                Core.Initialize();
            }

            string[] options = new[]
            {
                "--no-video-title-show", // Disable LibVLC default OSD banner
                "--mouse-events",
                "--keyboard-events"
            };

            _libVlc = new LibVLC(options);
            _mediaPlayer = new MediaPlayer(_libVlc);

            // Apply initial settings
            _mediaPlayer.Volume = _settingsService.Settings.Volume;
            _mediaPlayer.Mute = _settingsService.Settings.IsMuted;

            HookEvents();
            _logger.LogInformation("LibVLC and MediaPlayer successfully initialized");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to initialize LibVLC engine");
            throw;
        }
    }

    private void HookEvents()
    {
        if (_mediaPlayer == null) return;

        _mediaPlayer.Playing += (s, e) => Dispatch(() =>
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Volume = _settingsService.Settings.Volume;
                _mediaPlayer.Mute = _settingsService.Settings.IsMuted;
            }
            State = PlaybackState.Playing;
            StateChanged?.Invoke(this, State);
            RefreshTracks();
        });

        _mediaPlayer.Paused += (s, e) => Dispatch(() =>
        {
            State = PlaybackState.Paused;
            StateChanged?.Invoke(this, State);
        });

        _mediaPlayer.Stopped += (s, e) => Dispatch(() =>
        {
            State = PlaybackState.Stopped;
            StateChanged?.Invoke(this, State);
        });

        _mediaPlayer.EndReached += (s, e) => Dispatch(() =>
        {
            State = PlaybackState.Ended;
            StateChanged?.Invoke(this, State);
            MediaEnded?.Invoke(this, EventArgs.Empty);
        });

        _mediaPlayer.EncounteredError += (s, e) => Dispatch(() =>
        {
            State = PlaybackState.Error;
            string msg = "An error occurred while playing media.";
            _logger.LogError("LibVLC encountered playback error for {Path}", CurrentMediaPath);
            StateChanged?.Invoke(this, State);
            ErrorOccurred?.Invoke(this, msg);
        });

        _mediaPlayer.TimeChanged += (s, e) => Dispatch(() =>
        {
            PositionChanged?.Invoke(this, TimeSpan.FromMilliseconds(e.Time));
        });

        _mediaPlayer.LengthChanged += (s, e) => Dispatch(() =>
        {
            DurationChanged?.Invoke(this, TimeSpan.FromMilliseconds(e.Length));
            RefreshTracks();
        });
    }

    private void Dispatch(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(action);
        }
        else
        {
            action();
        }
    }

    public async Task<bool> OpenMediaAsync(string filePathOrUri)
    {
        if (_libVlc == null || _mediaPlayer == null) return false;

        try
        {
            _logger.LogInformation("Opening media: {Path}", filePathOrUri);

            Stop();

            _currentMedia?.Dispose();
            _currentMedia = null;

            bool isUri = Uri.TryCreate(filePathOrUri, UriKind.Absolute, out var uriResult)
                         && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

            if (isUri && uriResult != null)
            {
                _currentMedia = new Media(_libVlc, uriResult);
                CurrentMediaTitle = uriResult.Segments.LastOrDefault() ?? filePathOrUri;
            }
            else
            {
                if (!File.Exists(filePathOrUri))
                {
                    _logger.LogWarning("File not found: {Path}", filePathOrUri);
                    ErrorOccurred?.Invoke(this, $"File not found: {Path.GetFileName(filePathOrUri)}");
                    return false;
                }

                _currentMedia = new Media(_libVlc, filePathOrUri, FromType.FromPath);
                CurrentMediaTitle = Path.GetFileName(filePathOrUri);
            }

            CurrentMediaPath = filePathOrUri;
            State = PlaybackState.Opening;
            StateChanged?.Invoke(this, State);

            // Parse media asynchronously to extract metadata and track information
            await _currentMedia.Parse(MediaParseOptions.ParseLocal | MediaParseOptions.FetchLocal);

            _mediaPlayer.Media = _currentMedia;
            _mediaPlayer.SetRate(1.0f);
            _mediaPlayer.Volume = _settingsService.Settings.Volume;
            _mediaPlayer.Play();

            MediaOpened?.Invoke(this, filePathOrUri);

            // Auto-detect and load external subtitles if enabled
            if (_settingsService.Settings.AutoLoadSubtitles && !isUri)
            {
                TryAutoLoadExternalSubtitles(filePathOrUri);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while opening media {Path}", filePathOrUri);
            State = PlaybackState.Error;
            StateChanged?.Invoke(this, State);
            ErrorOccurred?.Invoke(this, $"Unable to open media: {ex.Message}");
            return false;
        }
    }

    private void TryAutoLoadExternalSubtitles(string videoFilePath)
    {
        try
        {
            string? dir = Path.GetDirectoryName(videoFilePath);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

            string baseName = Path.GetFileNameWithoutExtension(videoFilePath);
            string[] subExtensions = { ".srt", ".ass", ".ssa", ".vtt", ".sub" };

            foreach (var ext in subExtensions)
            {
                string exactSub = Path.Combine(dir, baseName + ext);
                if (File.Exists(exactSub))
                {
                    _logger.LogInformation("Auto-detected subtitle file: {SubPath}", exactSub);
                    AddSubtitleFile(exactSub);
                    return;
                }

                // Also check language variants like movie.en.srt
                var matchingFiles = Directory.GetFiles(dir, $"{baseName}.*{ext}");
                if (matchingFiles.Length > 0)
                {
                    _logger.LogInformation("Auto-detected matching subtitle file: {SubPath}", matchingFiles[0]);
                    AddSubtitleFile(matchingFiles[0]);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while auto-detecting subtitles for {Path}", videoFilePath);
        }
    }

    public void Play()
    {
        _mediaPlayer?.Play();
    }

    public void Pause()
    {
        _mediaPlayer?.SetPause(true);
    }

    public void TogglePlayPause()
    {
        if (_mediaPlayer == null) return;
        if (_mediaPlayer.IsPlaying)
        {
            _mediaPlayer.SetPause(true);
        }
        else
        {
            _mediaPlayer.Play();
        }
    }

    public void Stop()
    {
        if (_mediaPlayer != null && _mediaPlayer.IsPlaying)
        {
            _mediaPlayer.Stop();
        }
        State = PlaybackState.Stopped;
        StateChanged?.Invoke(this, State);
    }

    public void SeekTo(TimeSpan position)
    {
        if (_mediaPlayer == null) return;
        long targetMs = (long)position.TotalMilliseconds;
        long length = _mediaPlayer.Length;

        if (length > 0)
        {
            targetMs = Math.Clamp(targetMs, 0, length);
            _mediaPlayer.Time = targetMs;
        }
    }

    public void SeekToFraction(float fraction)
    {
        if (_mediaPlayer == null) return;
        float clamped = Math.Clamp(fraction, 0f, 1f);
        _mediaPlayer.Position = clamped;
    }

    public void SeekRelative(TimeSpan offset)
    {
        if (_mediaPlayer == null) return;
        long current = _mediaPlayer.Time;
        long delta = (long)offset.TotalMilliseconds;
        long target = Math.Max(0, current + delta);
        if (_mediaPlayer.Length > 0)
        {
            target = Math.Min(target, _mediaPlayer.Length);
        }
        _mediaPlayer.Time = target;
    }

    public bool TakeSnapshot(string outputPath, uint width = 0, uint height = 0)
    {
        if (_mediaPlayer == null || !_mediaPlayer.IsPlaying) return false;

        try
        {
            string? dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            return _mediaPlayer.TakeSnapshot(0, outputPath, width, height);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture snapshot to {Path}", outputPath);
            return false;
        }
    }

    public void SetSubtitleTrack(int trackId)
    {
        if (_mediaPlayer != null)
        {
            _mediaPlayer.SetSpu(trackId);
            RefreshTracks();
        }
    }

    public void SetAudioTrack(int trackId)
    {
        if (_mediaPlayer != null)
        {
            _mediaPlayer.SetAudioTrack(trackId);
            RefreshTracks();
        }
    }

    public void SetVideoTrack(int trackId)
    {
        if (_mediaPlayer != null)
        {
            _mediaPlayer.SetVideoTrack(trackId);
            RefreshTracks();
        }
    }

    public bool AddSubtitleFile(string subtitlePath)
    {
        if (_mediaPlayer == null || !File.Exists(subtitlePath)) return false;

        try
        {
            var uri = new Uri(subtitlePath);
            bool success = _mediaPlayer.AddSlave(MediaSlaveType.Subtitle, uri.AbsoluteUri, select: true);
            _logger.LogInformation("Added external subtitle {Path}, success={Success}", subtitlePath, success);
            RefreshTracks();
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add subtitle file {Path}", subtitlePath);
            return false;
        }
    }

    public void SetSubtitleDelay(long delayMicroseconds)
    {
        _mediaPlayer?.SetSpuDelay(delayMicroseconds);
    }

    public void SetAudioDelay(long delayMicroseconds)
    {
        _mediaPlayer?.SetAudioDelay(delayMicroseconds);
    }

    public void SetAspectRatio(string? aspectRatio)
    {
        if (_mediaPlayer != null)
        {
            _mediaPlayer.AspectRatio = string.Equals(aspectRatio, "Default", StringComparison.OrdinalIgnoreCase)
                ? null
                : aspectRatio;
        }
    }

    public void RefreshTracks()
    {
        if (_mediaPlayer == null) return;

        var info = new MediaTrackInfo();

        // Audio tracks
        var audioTracks = _mediaPlayer.AudioTrackDescription;
        if (audioTracks != null)
        {
            int currentAudio = _mediaPlayer.AudioTrack;
            info.SelectedAudioTrackId = currentAudio;
            foreach (var track in audioTracks)
            {
                info.AudioTracks.Add(new TrackItem
                {
                    Id = track.Id,
                    Name = track.Name ?? $"Audio Track {track.Id}",
                    IsSelected = track.Id == currentAudio
                });
            }
        }

        // Subtitle tracks
        var spuTracks = _mediaPlayer.SpuDescription;
        if (spuTracks != null)
        {
            int currentSpu = _mediaPlayer.Spu;
            info.SelectedSubtitleTrackId = currentSpu;
            foreach (var track in spuTracks)
            {
                info.SubtitleTracks.Add(new TrackItem
                {
                    Id = track.Id,
                    Name = track.Name ?? $"Subtitle {track.Id}",
                    IsSelected = track.Id == currentSpu
                });
            }
        }

        // Video tracks
        var videoTracks = _mediaPlayer.VideoTrackDescription;
        if (videoTracks != null)
        {
            int currentVideo = _mediaPlayer.VideoTrack;
            info.SelectedVideoTrackId = currentVideo;
            foreach (var track in videoTracks)
            {
                info.VideoTracks.Add(new TrackItem
                {
                    Id = track.Id,
                    Name = track.Name ?? $"Video Track {track.Id}",
                    IsSelected = track.Id == currentVideo
                });
            }
        }

        // Size & Codecs from current media track info
        if (_currentMedia != null)
        {
            var tracks = _currentMedia.Tracks;
            if (tracks != null)
            {
                foreach (var t in tracks)
                {
                    if (t.TrackType == TrackType.Video)
                    {
                        info.VideoWidth = t.Data.Video.Width;
                        info.VideoHeight = t.Data.Video.Height;
                        info.FrameRate = (float)t.Data.Video.FrameRateNum / Math.Max(1, t.Data.Video.FrameRateDen);
                        info.VideoCodec = FourCCToString(t.Codec);
                    }
                    else if (t.TrackType == TrackType.Audio)
                    {
                        info.AudioChannels = t.Data.Audio.Channels;
                        info.AudioSampleRate = t.Data.Audio.Rate;
                        info.AudioCodec = FourCCToString(t.Codec);
                    }
                }
            }
        }

        CurrentTrackInfo = info;
        TracksUpdated?.Invoke(this, info);
    }

    private static string FourCCToString(uint fourcc)
    {
        byte[] bytes = BitConverter.GetBytes(fourcc);
        return new string(bytes.Select(b => (b >= 32 && b <= 126) ? (char)b : ' ').ToArray()).Trim();
    }

    public MediaDetails? GetCurrentMediaDetails()
    {
        if (string.IsNullOrEmpty(CurrentMediaPath)) return null;

        var details = new MediaDetails
        {
            FilePath = CurrentMediaPath,
            FileName = CurrentMediaTitle ?? Path.GetFileName(CurrentMediaPath),
            Duration = Duration,
            Width = CurrentTrackInfo.VideoWidth,
            Height = CurrentTrackInfo.VideoHeight,
            VideoCodec = CurrentTrackInfo.VideoCodec,
            FrameRate = CurrentTrackInfo.FrameRate,
            AudioCodec = CurrentTrackInfo.AudioCodec,
            AudioChannels = CurrentTrackInfo.AudioChannels,
            AudioSampleRate = CurrentTrackInfo.AudioSampleRate,
            SubtitleTrackNames = CurrentTrackInfo.SubtitleTracks.Select(s => s.Name).ToList(),
            AudioTrackNames = CurrentTrackInfo.AudioTracks.Select(a => a.Name).ToList()
        };

        if (File.Exists(CurrentMediaPath))
        {
            var fileInfo = new FileInfo(CurrentMediaPath);
            details.FileSizeBytes = fileInfo.Length;
            details.Container = fileInfo.Extension.TrimStart('.').ToUpperInvariant();
        }

        return details;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _logger.LogInformation("Disposing VlcMediaPlayerService resources");

        try
        {
            _mediaPlayer?.Stop();
            _currentMedia?.Dispose();
            _mediaPlayer?.Dispose();
            _libVlc?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during LibVLC disposal");
        }
    }
}
