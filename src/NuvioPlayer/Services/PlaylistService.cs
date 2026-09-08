using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using NuvioPlayer.Interfaces;
using NuvioPlayer.Models;

namespace NuvioPlayer.Services;

public class PlaylistService : IPlaylistService
{
    private readonly ILogger<PlaylistService> _logger;
    private readonly Random _random = new();
    private int _currentIndex = -1;

    public ObservableCollection<PlaylistItem> Items { get; } = new();

    public PlaylistItem? CurrentItem =>
        _currentIndex >= 0 && _currentIndex < Items.Count ? Items[_currentIndex] : null;

    public int CurrentIndex => _currentIndex;

    public RepeatMode RepeatMode { get; set; } = RepeatMode.None;
    public bool IsShuffle { get; set; } = false;

    public event EventHandler<PlaylistItem?>? CurrentItemChanged;
    public event EventHandler? PlaylistModified;

    public PlaylistService(ILogger<PlaylistService> logger)
    {
        _logger = logger;
    }

    public void Add(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        var item = new PlaylistItem(filePath);
        Items.Add(item);
        _logger.LogInformation("Added to playlist: {Path}", filePath);
        PlaylistModified?.Invoke(this, EventArgs.Empty);
    }

    public void AddRange(IEnumerable<string> filePaths)
    {
        foreach (var path in filePaths)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                Items.Add(new PlaylistItem(path));
            }
        }
        PlaylistModified?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(PlaylistItem item)
    {
        int index = Items.IndexOf(item);
        if (index >= 0)
        {
            bool wasPlaying = item.IsPlaying;
            Items.RemoveAt(index);

            if (wasPlaying)
            {
                if (Items.Count > 0)
                {
                    _currentIndex = Math.Clamp(index, 0, Items.Count - 1);
                    Items[_currentIndex].IsPlaying = true;
                    CurrentItemChanged?.Invoke(this, Items[_currentIndex]);
                }
                else
                {
                    _currentIndex = -1;
                    CurrentItemChanged?.Invoke(this, null);
                }
            }
            else if (index < _currentIndex)
            {
                _currentIndex--;
            }

            PlaylistModified?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Clear()
    {
        foreach (var item in Items)
        {
            item.IsPlaying = false;
        }
        Items.Clear();
        _currentIndex = -1;
        CurrentItemChanged?.Invoke(this, null);
        PlaylistModified?.Invoke(this, EventArgs.Empty);
    }

    public void MoveItem(int oldIndex, int newIndex)
    {
        if (oldIndex >= 0 && oldIndex < Items.Count && newIndex >= 0 && newIndex < Items.Count)
        {
            var item = Items[oldIndex];
            Items.Move(oldIndex, newIndex);

            if (_currentIndex == oldIndex)
            {
                _currentIndex = newIndex;
            }
            else if (oldIndex < _currentIndex && newIndex >= _currentIndex)
            {
                _currentIndex--;
            }
            else if (oldIndex > _currentIndex && newIndex <= _currentIndex)
            {
                _currentIndex++;
            }

            PlaylistModified?.Invoke(this, EventArgs.Empty);
        }
    }

    public PlaylistItem? PlayAt(int index)
    {
        if (index >= 0 && index < Items.Count)
        {
            if (_currentIndex >= 0 && _currentIndex < Items.Count)
            {
                Items[_currentIndex].IsPlaying = false;
            }

            _currentIndex = index;
            var item = Items[_currentIndex];
            item.IsPlaying = true;

            CurrentItemChanged?.Invoke(this, item);
            return item;
        }

        return null;
    }

    public PlaylistItem? PlayItem(PlaylistItem item)
    {
        int index = Items.IndexOf(item);
        if (index >= 0)
        {
            return PlayAt(index);
        }
        return null;
    }

    public PlaylistItem? Next()
    {
        if (Items.Count == 0) return null;

        if (RepeatMode == RepeatMode.One && CurrentItem != null)
        {
            return CurrentItem;
        }

        if (IsShuffle && Items.Count > 1)
        {
            int nextIdx = _random.Next(0, Items.Count);
            if (nextIdx == _currentIndex)
            {
                nextIdx = (nextIdx + 1) % Items.Count;
            }
            return PlayAt(nextIdx);
        }

        int targetIndex = _currentIndex + 1;
        if (targetIndex < Items.Count)
        {
            return PlayAt(targetIndex);
        }

        if (RepeatMode == RepeatMode.All)
        {
            return PlayAt(0);
        }

        return null;
    }

    public PlaylistItem? Previous()
    {
        if (Items.Count == 0) return null;

        int targetIndex = _currentIndex - 1;
        if (targetIndex >= 0)
        {
            return PlayAt(targetIndex);
        }

        if (RepeatMode == RepeatMode.All)
        {
            return PlayAt(Items.Count - 1);
        }

        return PlayAt(0);
    }

    public async Task SaveM3uAsync(string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#EXTM3U");

        foreach (var item in Items)
        {
            int durationSec = (int)item.Duration.TotalSeconds;
            sb.AppendLine($"#EXTINF:{durationSec},{item.Title}");
            sb.AppendLine(item.FilePath);
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
        _logger.LogInformation("Saved playlist to {Path}", filePath);
    }

    public async Task LoadM3uAsync(string filePath)
    {
        if (!File.Exists(filePath)) return;

        var lines = await File.ReadAllLinesAsync(filePath);
        string? dir = Path.GetDirectoryName(filePath);
        var added = new List<string>();

        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

            string resolved = line;
            if (!Path.IsPathRooted(line) && !string.IsNullOrEmpty(dir))
            {
                resolved = Path.GetFullPath(Path.Combine(dir, line));
            }

            if (File.Exists(resolved))
            {
                added.Add(resolved);
            }
        }

        if (added.Count > 0)
        {
            AddRange(added);
            _logger.LogInformation("Loaded {Count} items from M3U playlist {Path}", added.Count, filePath);
        }
    }
}
