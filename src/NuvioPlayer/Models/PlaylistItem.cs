using CommunityToolkit.Mvvm.ComponentModel;

namespace NuvioPlayer.Models;

public partial class PlaylistItem : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private TimeSpan _duration = TimeSpan.Zero;

    [ObservableProperty]
    private string _durationText = "--:--";

    [ObservableProperty]
    private bool _isPlaying = false;

    [ObservableProperty]
    private bool _exists = true;

    public PlaylistItem()
    {
    }

    public PlaylistItem(string filePath)
    {
        FilePath = filePath;
        Title = System.IO.Path.GetFileName(filePath);
        Exists = System.IO.File.Exists(filePath);
    }
}
