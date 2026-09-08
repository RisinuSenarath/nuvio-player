using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NuvioPlayer.Database.Entities;
using NuvioPlayer.Interfaces;
using NuvioPlayer.Models;

namespace NuvioPlayer.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly ILogger<MainViewModel> _logger;
    private readonly ISettingsService _settingsService;
    private readonly IMediaPlayerService _mediaPlayerService;
    private readonly IPlaylistService _playlistService;
    private readonly IDatabaseService _databaseService;
    private readonly IDirectStreamResolverService _streamResolver;
    public PlaylistViewModel Playlist { get; }

    [ObservableProperty]
    private string _windowTitle = "Nuvio Player";

    [ObservableProperty]
    private string _mediaTitle = string.Empty;

    [ObservableProperty]
    private bool _isStreamingOnline;

    [ObservableProperty]
    private string _streamingUrl = string.Empty;

    [ObservableProperty]
    private string _streamingProvider = "vidlink";

    [ObservableProperty]
    private int _streamingTmdbId;

    [ObservableProperty]
    private string _streamingMediaType = "movie";

    [ObservableProperty]
    private int? _streamingSeason;

    [ObservableProperty]
    private int? _streamingEpisode;

    [ObservableProperty]
    private bool _hasMedia = false;

    [ObservableProperty]
    private bool _isPlaying = false;

    [ObservableProperty]
    private bool _isPaused = false;

    [ObservableProperty]
    private bool _isFullscreen = false;

    [ObservableProperty]
    private bool _isPlaylistOpen = false;

    [ObservableProperty]
    private bool _areControlsVisible = true;

    [ObservableProperty]
    private bool _isResumePromptVisible = false;

    [ObservableProperty]
    private string _resumePromptText = string.Empty;

    [ObservableProperty]
    private TimeSpan _position = TimeSpan.Zero;

    [ObservableProperty]
    private TimeSpan _duration = TimeSpan.Zero;

    [ObservableProperty]
    private double _positionSeconds = 0;

    [ObservableProperty]
    private double _durationSeconds = 0;

    [ObservableProperty]
    private string _positionText = "00:00";

    [ObservableProperty]
    private string _durationText = "00:00";

    [ObservableProperty]
    private int _volume = 100;

    [ObservableProperty]
    private bool _isMuted = false;

    [ObservableProperty]
    private double _playbackRate = 1.0;

    [ObservableProperty]
    private string _playbackRateText = "1.0x";

    [ObservableProperty]
    private string _aspectRatio = "Default";

    [ObservableProperty]
    private string _osdMessage = string.Empty;

    [ObservableProperty]
    private bool _isOsdVisible = false;

    private bool _isUserSeeking = false;
    private long _subtitleDelayMs = 0;
    private long _audioDelayMs = 0;
    private long _pendingResumePositionMs = 0;

    public ObservableCollection<TrackItem> SubtitleTracks { get; } = new();
    public ObservableCollection<TrackItem> AudioTracks { get; } = new();
    public ObservableCollection<TrackItem> VideoTracks { get; } = new();

    [ObservableProperty]
    private int _selectedSubtitleTrackId = -1;

    [ObservableProperty]
    private int _selectedAudioTrackId = -1;

    [ObservableProperty]
    private int _selectedVideoTrackId = -1;

    [ObservableProperty]
    private string _currentSubtitleTrackName = "None";

    [ObservableProperty]
    private string _currentAudioTrackName = "Default";

    public ObservableCollection<MediaHistoryEntity> RecentMedia { get; } = new();
    public ObservableCollection<BookmarkEntity> Bookmarks { get; } = new();
    public bool HasBookmarks => Bookmarks.Count > 0;

    [ObservableProperty]
    private bool _isBookmarksOpen = false;

    [ObservableProperty]
    private bool _hasRecentMedia = false;

    public bool IsRightSidebarOpen => IsPlaylistOpen || IsBookmarksOpen;

    [ObservableProperty]
    private string _newBookmarkLabel = string.Empty;

    public LibVLCSharp.Shared.MediaPlayer? MediaPlayer => _mediaPlayerService.NativePlayer as LibVLCSharp.Shared.MediaPlayer;
    public IMediaPlayerService MediaPlayerService => _mediaPlayerService;

    public event Action? RequestClose;
    public event Action? RequestMinimize;
    public event Action? RequestMaximize;
    public event Action? RequestOpenFile;
    public event Action? RequestOpenFolder;
    public event Action? RequestOpenSettings;
    public event Action? RequestOpenMediaInfo;
    public event Action? RequestOpenStreaming;
    public event Action<string>? RequestNavigateWebStream;
    public event Action? RequestStopWebStream;

    public MainViewModel(
        ILogger<MainViewModel> logger,
        ISettingsService settingsService,
        IMediaPlayerService mediaPlayerService,
        IPlaylistService playlistService,
        PlaylistViewModel playlist,
        IDatabaseService databaseService,
        IDirectStreamResolverService? streamResolver = null)
    {
        _logger = logger;
        _settingsService = settingsService;
        _mediaPlayerService = mediaPlayerService;
        _playlistService = playlistService;
        Playlist = playlist;
        _databaseService = databaseService;
        _streamResolver = streamResolver ?? new NuvioPlayer.Services.DirectStreamResolverService();

        _volume = _settingsService.Settings.Volume;
        _isMuted = _settingsService.Settings.IsMuted;
        _playbackRate = _settingsService.Settings.DefaultPlaybackSpeed;
        _playbackRateText = $"{_playbackRate:0.0#}x";

        Playlist.ShowNotification += msg => ShowOsd(msg, 1500);
        Bookmarks.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasBookmarks));

        HookMediaEvents();
        _ = LoadRecentMediaAsync();
    }

    private void HookMediaEvents()
    {
        _mediaPlayerService.StateChanged += (s, state) =>
        {
            IsPlaying = state == Models.PlaybackState.Playing;
            IsPaused = state == Models.PlaybackState.Paused;
            HasMedia = state != Models.PlaybackState.Stopped && !string.IsNullOrEmpty(_mediaPlayerService.CurrentMediaPath);
        };

        _mediaPlayerService.PositionChanged += (s, pos) =>
        {
            if (!_isUserSeeking)
            {
                Position = pos;
                PositionSeconds = pos.TotalSeconds;
                PositionText = FormatTime(pos);

                if ((long)pos.TotalSeconds % 5 == 0 && !string.IsNullOrEmpty(_mediaPlayerService.CurrentMediaPath))
                {
                    _ = _databaseService.UpdatePositionAsync(_mediaPlayerService.CurrentMediaPath, (long)pos.TotalMilliseconds);
                }
            }
        };

        _mediaPlayerService.DurationChanged += (s, dur) =>
        {
            Duration = dur;
            DurationSeconds = dur.TotalSeconds;
            DurationText = FormatTime(dur);
        };

        _mediaPlayerService.MediaOpened += (s, path) =>
        {
            HasMedia = true;
            if (string.IsNullOrEmpty(MediaTitle) || !path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                MediaTitle = _mediaPlayerService.CurrentMediaTitle ?? System.IO.Path.GetFileName(path);
            }
            WindowTitle = $"{MediaTitle} — Nuvio Player";
            ShowOsd($"Playing: {MediaTitle}");
        };

        _mediaPlayerService.ErrorOccurred += (s, err) =>
        {
            ShowOsd(err, 4000);
        };

        _mediaPlayerService.MediaEnded += async (s, e) =>
        {
            if (!string.IsNullOrEmpty(_mediaPlayerService.CurrentMediaPath))
            {
                await _databaseService.UpdatePositionAsync(_mediaPlayerService.CurrentMediaPath, 0);
            }
            IsResumePromptVisible = false;
            Playlist.NextCommand.Execute(null);
        };

        _mediaPlayerService.TracksUpdated += (s, trackInfo) =>
        {
            void Update()
            {
                SubtitleTracks.Clear();
                foreach (var t in trackInfo.SubtitleTracks)
                {
                    SubtitleTracks.Add(t);
                }

                AudioTracks.Clear();
                foreach (var a in trackInfo.AudioTracks)
                {
                    AudioTracks.Add(a);
                }

                VideoTracks.Clear();
                foreach (var v in trackInfo.VideoTracks)
                {
                    VideoTracks.Add(v);
                }

                SelectedSubtitleTrackId = trackInfo.SelectedSubtitleTrackId;
                var curSub = trackInfo.SubtitleTracks.FirstOrDefault(t => t.Id == SelectedSubtitleTrackId);
                CurrentSubtitleTrackName = curSub != null ? curSub.Name : "None";

                SelectedAudioTrackId = trackInfo.SelectedAudioTrackId;
                var curAud = trackInfo.AudioTracks.FirstOrDefault(t => t.Id == SelectedAudioTrackId);
                CurrentAudioTrackName = curAud != null ? curAud.Name : "Default";

                SelectedVideoTrackId = trackInfo.SelectedVideoTrackId;
            }

            if (System.Windows.Application.Current?.Dispatcher != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                _ = System.Windows.Application.Current.Dispatcher.BeginInvoke((Action)Update);
            }
            else
            {
                Update();
            }
        };
    }

    [RelayCommand]
    public void ResumePlayback()
    {
        IsResumePromptVisible = false;
        if (_pendingResumePositionMs > 0 && HasMedia)
        {
            _mediaPlayerService.SeekTo(TimeSpan.FromMilliseconds(_pendingResumePositionMs));
            ShowOsd($"Resumed at {FormatTime(TimeSpan.FromMilliseconds(_pendingResumePositionMs))}", 1500);
        }
    }

    [RelayCommand]
    public void StartOverPlayback()
    {
        IsResumePromptVisible = false;
        if (HasMedia)
        {
            _mediaPlayerService.SeekTo(TimeSpan.Zero);
            ShowOsd("Started from beginning", 1200);
        }
    }

    [RelayCommand]
    public void DismissResumePrompt()
    {
        IsResumePromptVisible = false;
    }

    [RelayCommand]
    public void Next() => Playlist.NextCommand.Execute(null);

    [RelayCommand]
    public void Previous() => Playlist.PreviousCommand.Execute(null);

    [RelayCommand]
    private void Minimize() => RequestMinimize?.Invoke();

    [RelayCommand]
    private void Maximize() => RequestMaximize?.Invoke();

    [RelayCommand]
    private void Close() => RequestClose?.Invoke();

    [RelayCommand]
    private void OpenFile() => RequestOpenFile?.Invoke();

    [RelayCommand]
    private void OpenFolder() => RequestOpenFolder?.Invoke();

    [RelayCommand]
    private void OpenSettings() => RequestOpenSettings?.Invoke();

    [RelayCommand]
    private void OpenMediaInfo() => RequestOpenMediaInfo?.Invoke();

    [RelayCommand]
    private void OpenStreaming() => RequestOpenStreaming?.Invoke();

    [RelayCommand]
    public void TogglePlayPause()
    {
        if (!HasMedia)
        {
            OpenFile();
            return;
        }

        _mediaPlayerService.TogglePlayPause();
        ShowOsd(_mediaPlayerService.IsPlaying ? "Play" : "Pause", 1200);
    }

    [RelayCommand]
    public void Stop()
    {
        _mediaPlayerService.Stop();
        HasMedia = false;
        MediaTitle = string.Empty;
        WindowTitle = "Nuvio Player";
        ShowOsd("Stopped", 1200);
    }

    [RelayCommand]
    public void TogglePlaylist()
    {
        if (IsPlaylistOpen)
        {
            IsPlaylistOpen = false;
        }
        else
        {
            IsPlaylistOpen = true;
            IsBookmarksOpen = false;
        }
        OnPropertyChanged(nameof(IsRightSidebarOpen));
        ShowOsd(IsPlaylistOpen ? "Playlist Open" : "Sidebar Closed", 1200);
    }

    [RelayCommand]
    public void CloseSidebar()
    {
        IsPlaylistOpen = false;
        IsBookmarksOpen = false;
        OnPropertyChanged(nameof(IsRightSidebarOpen));
    }

    [RelayCommand]
    public void ShowPlaylistTab()
    {
        IsPlaylistOpen = true;
        IsBookmarksOpen = false;
        OnPropertyChanged(nameof(IsRightSidebarOpen));
    }

    [RelayCommand]
    public void ShowBookmarksTab()
    {
        IsBookmarksOpen = true;
        IsPlaylistOpen = false;
        OnPropertyChanged(nameof(IsRightSidebarOpen));
    }

    [RelayCommand]
    public void ToggleFullscreen()
    {
        IsFullscreen = !IsFullscreen;
        ShowOsd(IsFullscreen ? "Fullscreen" : "Windowed", 1200);
    }

    [RelayCommand]
    public void ToggleMute()
    {
        IsMuted = !IsMuted;
        _mediaPlayerService.IsMuted = IsMuted;
        ShowOsd(IsMuted ? "Muted" : $"Volume {Volume}%", 1200);
    }

    partial void OnVolumeChanged(int value)
    {
        int clamped = Math.Clamp(value, 0, 150);
        if (_mediaPlayerService.Volume != clamped)
        {
            _mediaPlayerService.Volume = clamped;
        }
    }

    [RelayCommand]
    public void ChangeVolume(int delta)
    {
        int newVolume = Math.Clamp(Volume + delta, 0, 150);
        Volume = newVolume;
        _mediaPlayerService.Volume = newVolume;
        if (IsMuted && newVolume > 0)
        {
            IsMuted = false;
            _mediaPlayerService.IsMuted = false;
        }
        string boost = Volume > 100 ? " (Boost)" : "";
        ShowOsd($"Volume {Volume}%{boost}", 1200);
    }

    [RelayCommand]
    public void SetVolumeDirect(int value)
    {
        Volume = Math.Clamp(value, 0, 150);
        _mediaPlayerService.Volume = Volume;
    }

    [RelayCommand]
    public void SeekRelative(object secondsParam)
    {
        if (!HasMedia) return;

        int seconds = 5;
        if (secondsParam is int i)
        {
            seconds = i;
        }
        else if (secondsParam is string s && int.TryParse(s, out int parsed))
        {
            seconds = parsed;
        }

        double maxSeconds = DurationSeconds > 0 ? DurationSeconds : double.MaxValue;
        double targetSeconds = Math.Clamp(PositionSeconds + seconds, 0, maxSeconds);
        Position = TimeSpan.FromSeconds(targetSeconds);
        PositionSeconds = targetSeconds;
        PositionText = FormatTime(Position);

        _mediaPlayerService.SeekRelative(TimeSpan.FromSeconds(seconds));

        string prefix = seconds > 0 ? $"+{seconds}" : $"{seconds}";
        ShowOsd($"{prefix} sec", 1200);
    }

    public void BeginUserSeek()
    {
        _isUserSeeking = true;
    }

    public void EndUserSeek(double targetSeconds)
    {
        _isUserSeeking = false;
        if (HasMedia)
        {
            _mediaPlayerService.SeekTo(TimeSpan.FromSeconds(targetSeconds));
            Position = TimeSpan.FromSeconds(targetSeconds);
            PositionSeconds = targetSeconds;
            PositionText = FormatTime(Position);
            ShowOsd($"Seek: {PositionText}", 1200);
        }
    }

    [RelayCommand]
    public void SetPlaybackRate(object rateParam)
    {
        double rate = 1.0;
        if (rateParam is double d)
        {
            rate = d;
        }
        else if (rateParam is string s && double.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out double parsed))
        {
            rate = parsed;
        }

        PlaybackRate = rate;
        _mediaPlayerService.PlaybackRate = rate;
        PlaybackRateText = $"{rate:0.0#}x";
        ShowOsd($"Speed: {PlaybackRateText}", 1500);
    }

    [RelayCommand]
    public void CyclePlaybackRate()
    {
        double[] speeds = { 0.5, 0.75, 1.0, 1.25, 1.5, 2.0 };
        int idx = Array.FindIndex(speeds, s => Math.Abs(s - PlaybackRate) < 0.01);
        int nextIdx = (idx + 1) % speeds.Length;
        SetPlaybackRate(speeds[nextIdx]);
    }

    [RelayCommand]
    public void SetAspectRatio(string ratio)
    {
        AspectRatio = ratio;
        _mediaPlayerService.SetAspectRatio(ratio);
        ShowOsd($"Aspect Ratio: {ratio}", 1500);
    }

    [RelayCommand]
    public void AdjustSubtitleDelay(object deltaParam)
    {
        int deltaMs = 50;
        if (deltaParam is int i) deltaMs = i;
        else if (deltaParam is string s && int.TryParse(s, out int parsed)) deltaMs = parsed;

        _subtitleDelayMs += deltaMs;
        _mediaPlayerService.SetSubtitleDelay(_subtitleDelayMs * 1000); // microseconds
        string sign = _subtitleDelayMs >= 0 ? "+" : "";
        ShowOsd($"Subtitle Delay: {sign}{_subtitleDelayMs} ms", 1500);
    }

    [RelayCommand]
    public void ResetSubtitleDelay()
    {
        _subtitleDelayMs = 0;
        _mediaPlayerService.SetSubtitleDelay(0);
        ShowOsd("Subtitle Delay: 0 ms", 1500);
    }

    [RelayCommand]
    public void AdjustAudioDelay(object deltaParam)
    {
        int deltaMs = 50;
        if (deltaParam is int i) deltaMs = i;
        else if (deltaParam is string s && int.TryParse(s, out int parsed)) deltaMs = parsed;

        _audioDelayMs += deltaMs;
        _mediaPlayerService.SetAudioDelay(_audioDelayMs * 1000); // microseconds
        string sign = _audioDelayMs >= 0 ? "+" : "";
        ShowOsd($"Audio Delay: {sign}{_audioDelayMs} ms", 1500);
    }

    [RelayCommand]
    public void ResetAudioDelay()
    {
        _audioDelayMs = 0;
        _mediaPlayerService.SetAudioDelay(0);
        ShowOsd("Audio Delay: 0 ms", 1500);
    }

    [RelayCommand]
    public void SelectSubtitleTrack(int trackId)
    {
        SelectedSubtitleTrackId = trackId;
        _mediaPlayerService.SetSubtitleTrack(trackId);
        if (trackId == -1)
        {
            CurrentSubtitleTrackName = "Disabled";
            ShowOsd("Subtitles: Disabled", 1500);
        }
        else
        {
            var match = SubtitleTracks.FirstOrDefault(t => t.Id == trackId);
            CurrentSubtitleTrackName = match?.Name ?? $"Track {trackId}";
            ShowOsd($"Subtitles: {CurrentSubtitleTrackName}", 1500);
        }
    }

    [RelayCommand]
    public void ToggleSubtitles()
    {
        if (SubtitleTracks.Count == 0)
        {
            ShowOsd("No subtitle tracks available", 1500);
            return;
        }

        if (SelectedSubtitleTrackId == -1)
        {
            var first = SubtitleTracks.FirstOrDefault(t => t.Id != -1) ?? SubtitleTracks[0];
            SelectSubtitleTrack(first.Id);
        }
        else
        {
            int currentIdx = -1;
            for (int i = 0; i < SubtitleTracks.Count; i++)
            {
                if (SubtitleTracks[i].Id == SelectedSubtitleTrackId)
                {
                    currentIdx = i;
                    break;
                }
            }

            if (currentIdx >= 0 && currentIdx + 1 < SubtitleTracks.Count)
            {
                SelectSubtitleTrack(SubtitleTracks[currentIdx + 1].Id);
            }
            else
            {
                SelectSubtitleTrack(-1);
            }
        }
    }

    [RelayCommand]
    public void SelectAudioTrack(int trackId)
    {
        SelectedAudioTrackId = trackId;
        _mediaPlayerService.SetAudioTrack(trackId);
        if (trackId == -1)
        {
            CurrentAudioTrackName = "Disabled";
            ShowOsd("Audio: Disabled", 1500);
        }
        else
        {
            var match = AudioTracks.FirstOrDefault(t => t.Id == trackId);
            CurrentAudioTrackName = match?.Name ?? $"Track {trackId}";
            ShowOsd($"Audio: {CurrentAudioTrackName}", 1500);
        }
    }

    [RelayCommand]
    public void SelectVideoTrack(int trackId)
    {
        SelectedVideoTrackId = trackId;
        _mediaPlayerService.SetVideoTrack(trackId);
        var match = VideoTracks.FirstOrDefault(t => t.Id == trackId);
        string name = match?.Name ?? $"Track {trackId}";
        ShowOsd($"Video: {name}", 1500);
    }

    [RelayCommand]
    public void LoadSubtitleFile(string? filePath = null)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Subtitle File",
                Filter = "Subtitle Files (*.srt;*.ass;*.ssa;*.vtt;*.sub)|*.srt;*.ass;*.ssa;*.vtt;*.sub|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                filePath = dialog.FileName;
            }
            else
            {
                return;
            }
        }

        if (System.IO.File.Exists(filePath))
        {
            bool success = _mediaPlayerService.AddSubtitleFile(filePath);
            if (success)
            {
                ShowOsd($"Subtitle loaded: {System.IO.Path.GetFileName(filePath)}", 2500);
            }
            else
            {
                ShowOsd("Failed to load subtitle file", 2000);
            }
        }
    }

    [RelayCommand]
    public void TakeScreenshot()
    {
        if (!HasMedia) return;

        string dir = _settingsService.Settings.ScreenshotDirectory;
        if (string.IsNullOrEmpty(dir))
        {
            dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "Nuvio Screenshots");
        }

        string safeName = System.IO.Path.GetFileNameWithoutExtension(_mediaPlayerService.CurrentMediaPath ?? "media");
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string fileName = $"{safeName}_{timestamp}.png";
        string fullPath = System.IO.Path.Combine(dir, fileName);

        bool success = _mediaPlayerService.TakeSnapshot(fullPath);
        if (success)
        {
            ShowOsd($"Screenshot saved: {fileName}", 3000);
        }
        else
        {
            ShowOsd("Failed to capture screenshot", 2500);
        }
    }

    public async Task HandleMediaPathsAsync(IReadOnlyList<string> mediaPaths)
    {
        if (mediaPaths == null || mediaPaths.Count == 0) return;

        if (mediaPaths.Count == 1)
        {
            await OpenMediaFileAsync(mediaPaths[0]);
        }
        else
        {
            Playlist.Clear();
            Playlist.AddRange(mediaPaths);
            Playlist.PlayAt(0);
            IsPlaylistOpen = true;
            OnPropertyChanged(nameof(IsRightSidebarOpen));
            ShowOsd($"Playing {System.IO.Path.GetFileName(mediaPaths[0])} ({mediaPaths.Count} items queued)", 2000);
        }
    }

    [RelayCommand]
    public async Task OpenMediaFileAsync(string filePath)
    {
        CloseOnlineStream();
        SetPlaybackRate(1.0);
        await _mediaPlayerService.OpenMediaAsync(filePath);
        await CheckAndPromptResumeAsync(filePath);
    }

    public async Task StartWebStreamingAsync(
        int tmdbId,
        string mediaType,
        int? season,
        int? episode,
        string title,
        string provider = "vidlink")
    {
        _mediaPlayerService.Stop();
        HasMedia = false;

        StreamingTmdbId = tmdbId;
        StreamingMediaType = mediaType;
        StreamingSeason = season;
        StreamingEpisode = episode;
        StreamingProvider = provider;
        MediaTitle = title;
        WindowTitle = $"{title} — Nuvio Player";

        string url = _streamResolver.GetEmbedFallbackUrl(tmdbId, mediaType, season, episode, provider);
        StreamingUrl = url;
        IsStreamingOnline = true;

        RequestNavigateWebStream?.Invoke(url);
        ShowOsd($"Streaming online: {title}", 3000);
    }

    [RelayCommand]
    public void SwitchStreamingProvider(string newProvider)
    {
        if (StreamingTmdbId == 0) return;
        StreamingProvider = newProvider;
        string url = _streamResolver.GetEmbedFallbackUrl(
            StreamingTmdbId,
            StreamingMediaType,
            StreamingSeason,
            StreamingEpisode,
            newProvider);

        StreamingUrl = url;
        RequestNavigateWebStream?.Invoke(url);
        ShowOsd($"Switched server to {newProvider.ToUpperInvariant()}", 2000);
    }

    [RelayCommand]
    public void CloseOnlineStream()
    {
        IsStreamingOnline = false;
        StreamingUrl = string.Empty;
        StreamingTmdbId = 0;
        MediaTitle = string.Empty;
        WindowTitle = "Nuvio Player";
        RequestStopWebStream?.Invoke();
    }

    public async Task PlayOnlineStreamAsync(string streamUrl, string title)
    {
        if (IsEmbedUrl(streamUrl))
        {
            // Redirect embed URLs to in-player web streaming instead of LibVLC
            _mediaPlayerService.Stop();
            HasMedia = false;
            MediaTitle = title;
            WindowTitle = $"{title} — Nuvio Player";
            StreamingUrl = streamUrl;
            IsStreamingOnline = true;
            RequestNavigateWebStream?.Invoke(streamUrl);
            ShowOsd($"Streaming online: {title}", 3000);
            return;
        }

        CloseOnlineStream();
        SetPlaybackRate(1.0);
        MediaTitle = title;
        WindowTitle = $"{title} — Nuvio Player";
        await _mediaPlayerService.OpenMediaAsync(streamUrl);
        HasMedia = true;
        ShowOsd($"Streaming: {title}", 3000);
    }

    private static bool IsEmbedUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        string lower = url.ToLowerInvariant();
        return lower.Contains("vidlink.pro") ||
               lower.Contains("vidsrc") ||
               lower.Contains("multiembed.mov") ||
               lower.Contains("autoembed") ||
               lower.Contains("vidking.net") ||
               lower.Contains("embed.su") ||
               lower.Contains("superembed");
    }

    public async Task CheckAndPromptResumeAsync(string path)
    {
        // Record playback in SQLite history
        await _databaseService.RecordPlaybackAsync(
            path,
            MediaTitle,
            (long)_mediaPlayerService.Position.TotalMilliseconds,
            (long)_mediaPlayerService.Duration.TotalMilliseconds);

        // Check if we should prompt to resume
        if (_settingsService.Settings.ResumePlayback)
        {
            var history = await _databaseService.GetHistoryForFileAsync(path);
            if (history != null && history.DurationMs > 30000 && history.LastPositionMs > 10000)
            {
                if (history.LastPositionMs < history.DurationMs - 15000 && (double)history.LastPositionMs / history.DurationMs < 0.95)
                {
                    _pendingResumePositionMs = history.LastPositionMs;
                    ResumePromptText = $"Resume from {FormatTime(TimeSpan.FromMilliseconds(history.LastPositionMs))}?";
                    IsResumePromptVisible = true;
                }
            }
        }

        await RefreshBookmarksAsync(path);
        _ = LoadRecentMediaAsync();
    }

    public async Task LoadRecentMediaAsync()
    {
        try
        {
            var recents = await _databaseService.GetRecentMediaAsync(15);
            void Update()
            {
                RecentMedia.Clear();
                foreach (var r in recents)
                {
                    RecentMedia.Add(r);
                }
                HasRecentMedia = RecentMedia.Count > 0;
            }

            if (System.Windows.Application.Current?.Dispatcher != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                _ = System.Windows.Application.Current.Dispatcher.BeginInvoke((Action)Update);
            }
            else
            {
                Update();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load recent media");
        }
    }

    [RelayCommand]
    public async Task OpenRecentMedia(MediaHistoryEntity? item)
    {
        if (item == null) return;
        if (!System.IO.File.Exists(item.FilePath))
        {
            ShowOsd($"File no longer exists: {item.DisplayName}", 3000);
            return;
        }
        await OpenMediaFileAsync(item.FilePath);
    }

    [RelayCommand]
    public async Task RemoveRecentMedia(MediaHistoryEntity? item)
    {
        if (item == null) return;
        await _databaseService.RemoveHistoryItemAsync(item.Id);
        RecentMedia.Remove(item);
        HasRecentMedia = RecentMedia.Count > 0;
        ShowOsd("Removed from history", 1500);
    }

    [RelayCommand]
    public async Task ClearRecentMedia()
    {
        await _databaseService.ClearHistoryAsync();
        RecentMedia.Clear();
        HasRecentMedia = false;
        ShowOsd("Playback history cleared", 2000);
    }

    public async Task RefreshBookmarksAsync(string filePath)
    {
        try
        {
            var bms = await _databaseService.GetBookmarksForFileAsync(filePath);
            void Update()
            {
                Bookmarks.Clear();
                foreach (var b in bms)
                {
                    Bookmarks.Add(b);
                }
            }

            if (System.Windows.Application.Current?.Dispatcher != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                _ = System.Windows.Application.Current.Dispatcher.BeginInvoke((Action)Update);
            }
            else
            {
                Update();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load bookmarks for {Path}", filePath);
        }
    }

    [RelayCommand]
    public void ToggleBookmarks()
    {
        IsBookmarksOpen = !IsBookmarksOpen;
        if (IsBookmarksOpen && IsPlaylistOpen)
        {
            IsPlaylistOpen = false;
        }
    }

    [RelayCommand]
    public async Task AddBookmarkAsync(string? customLabel = null)
    {
        if (!HasMedia || string.IsNullOrEmpty(_mediaPlayerService.CurrentMediaPath)) return;

        long posMs = (long)_mediaPlayerService.Position.TotalMilliseconds;
        string label = !string.IsNullOrWhiteSpace(customLabel)
            ? customLabel.Trim()
            : (!string.IsNullOrWhiteSpace(NewBookmarkLabel)
                ? NewBookmarkLabel.Trim()
                : $"Bookmark at {FormatTime(_mediaPlayerService.Position)}");

        NewBookmarkLabel = string.Empty;

        var bm = await _databaseService.AddBookmarkAsync(_mediaPlayerService.CurrentMediaPath, posMs, label);
        Bookmarks.Add(bm);
        ShowOsd($"Bookmark saved: {label}", 2000);
    }

    [RelayCommand]
    public void JumpToBookmark(BookmarkEntity? bm)
    {
        if (bm == null || !HasMedia) return;
        _mediaPlayerService.SeekTo(TimeSpan.FromMilliseconds(bm.PositionMs));
        ShowOsd($"Jumped to {bm.Label}", 1500);
    }

    [RelayCommand]
    public async Task RemoveBookmarkAsync(BookmarkEntity? bm)
    {
        if (bm == null) return;
        await _databaseService.RemoveBookmarkAsync(bm.Id);
        Bookmarks.Remove(bm);
        ShowOsd("Bookmark removed", 1500);
    }

    public void ShowOsd(string message, int durationMs = 2000)
    {
        OsdMessage = message;
        IsOsdVisible = true;

        Task.Delay(durationMs).ContinueWith(_ =>
        {
            if (OsdMessage == message)
            {
                IsOsdVisible = false;
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private static string FormatTime(TimeSpan span)
    {
        if (span.TotalHours >= 1)
        {
            return $"{(int)span.TotalHours}:{span.Minutes:D2}:{span.Seconds:D2}";
        }
        return $"{span.Minutes:D2}:{span.Seconds:D2}";
    }
}
