using System.Collections.ObjectModel;
using NuvioPlayer.Models;

namespace NuvioPlayer.Interfaces;

public interface IPlaylistService
{
    ObservableCollection<PlaylistItem> Items { get; }
    PlaylistItem? CurrentItem { get; }
    int CurrentIndex { get; }
    RepeatMode RepeatMode { get; set; }
    bool IsShuffle { get; set; }

    event EventHandler<PlaylistItem?>? CurrentItemChanged;
    event EventHandler? PlaylistModified;

    void Add(string filePath);
    void AddRange(IEnumerable<string> filePaths);
    void Remove(PlaylistItem item);
    void Clear();
    void MoveItem(int oldIndex, int newIndex);
    PlaylistItem? PlayAt(int index);
    PlaylistItem? PlayItem(PlaylistItem item);
    PlaylistItem? Next();
    PlaylistItem? Previous();
    Task SaveM3uAsync(string filePath);
    Task LoadM3uAsync(string filePath);
}
