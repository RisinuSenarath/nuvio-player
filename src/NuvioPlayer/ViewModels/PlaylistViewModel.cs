using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NuvioPlayer.Interfaces;
using NuvioPlayer.Models;

namespace NuvioPlayer.ViewModels;

public partial class PlaylistViewModel : ViewModelBase
{
    private readonly IPlaylistService _playlistService;
    private readonly IMediaPlayerService _mediaPlayerService;

    public ObservableCollection<PlaylistItem> Items => _playlistService.Items;
    public bool HasItems => Items.Count > 0;

    [ObservableProperty]
    private PlaylistItem? _currentItem;

    [ObservableProperty]
    private bool _isShuffle;

    [ObservableProperty]
    private RepeatMode _repeatMode;

    [ObservableProperty]
    private string _repeatModeText = "Repeat: Off";

    public event Action<string>? ShowNotification;

    public PlaylistViewModel(IPlaylistService playlistService, IMediaPlayerService mediaPlayerService)
    {
        _playlistService = playlistService;
        _mediaPlayerService = mediaPlayerService;

        Items.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasItems));

        _playlistService.CurrentItemChanged += (s, item) =>
        {
            CurrentItem = item;
            if (item != null)
            {
                _mediaPlayerService.OpenMediaAsync(item.FilePath);
            }
        };

        UpdateRepeatText();
    }

    public void Add(string filePath) => _playlistService.Add(filePath);

    public void AddRange(IEnumerable<string> filePaths) => _playlistService.AddRange(filePaths);

    public PlaylistItem? PlayAt(int index) => _playlistService.PlayAt(index);

    [RelayCommand]
    public void PlayItem(PlaylistItem? item)
    {
        if (item != null)
        {
            _playlistService.PlayItem(item);
        }
    }

    [RelayCommand]
    public void RemoveItem(PlaylistItem? item)
    {
        if (item != null)
        {
            _playlistService.Remove(item);
        }
    }

    [RelayCommand]
    public void Clear()
    {
        _playlistService.Clear();
        ShowNotification?.Invoke("Playlist cleared");
    }

    [RelayCommand]
    public void ToggleShuffle()
    {
        IsShuffle = !IsShuffle;
        _playlistService.IsShuffle = IsShuffle;
        ShowNotification?.Invoke(IsShuffle ? "Shuffle On" : "Shuffle Off");
    }

    [RelayCommand]
    public void CycleRepeatMode()
    {
        RepeatMode = RepeatMode switch
        {
            RepeatMode.None => RepeatMode.All,
            RepeatMode.All => RepeatMode.One,
            RepeatMode.One => RepeatMode.None,
            _ => RepeatMode.None
        };

        _playlistService.RepeatMode = RepeatMode;
        UpdateRepeatText();
        ShowNotification?.Invoke(RepeatModeText);
    }

    private void UpdateRepeatText()
    {
        RepeatModeText = RepeatMode switch
        {
            RepeatMode.None => "Repeat: Off",
            RepeatMode.All => "Repeat: All",
            RepeatMode.One => "Repeat: One",
            _ => "Repeat: Off"
        };
    }

    [RelayCommand]
    public void Next()
    {
        var next = _playlistService.Next();
        if (next == null)
        {
            _mediaPlayerService.Stop();
        }
    }

    [RelayCommand]
    public void Previous()
    {
        _playlistService.Previous();
    }

    [RelayCommand]
    public void AddFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add to Playlist — Nuvio Player",
            Filter = "Media Files|*.mp4;*.mkv;*.avi;*.mov;*.webm;*.ts;*.m2ts;*.mp3;*.flac;*.wav;*.ogg;*.m4a|All Files (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true && dialog.FileNames.Length > 0)
        {
            _playlistService.AddRange(dialog.FileNames);
            ShowNotification?.Invoke($"Added {dialog.FileNames.Length} items to playlist");

            if (_playlistService.CurrentItem == null)
            {
                _playlistService.PlayAt(0);
            }
        }
    }

    [RelayCommand]
    public async Task SavePlaylistAsync()
    {
        if (_playlistService.Items.Count == 0)
        {
            ShowNotification?.Invoke("Playlist is empty");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Save Playlist — Nuvio Player",
            Filter = "M3U Playlist (*.m3u)|*.m3u|M3U8 Playlist (*.m3u8)|*.m3u8",
            DefaultExt = ".m3u"
        };

        if (dialog.ShowDialog() == true)
        {
            await _playlistService.SaveM3uAsync(dialog.FileName);
            ShowNotification?.Invoke($"Playlist saved: {System.IO.Path.GetFileName(dialog.FileName)}");
        }
    }

    [RelayCommand]
    public async Task LoadPlaylistAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Load Playlist — Nuvio Player",
            Filter = "Playlists (*.m3u;*.m3u8)|*.m3u;*.m3u8|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            await _playlistService.LoadM3uAsync(dialog.FileName);
            ShowNotification?.Invoke($"Loaded playlist: {System.IO.Path.GetFileName(dialog.FileName)}");
        }
    }
}
