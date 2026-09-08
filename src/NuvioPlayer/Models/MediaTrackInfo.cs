namespace NuvioPlayer.Models;

public class TrackItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Codec { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}

public class MediaTrackInfo
{
    public List<TrackItem> AudioTracks { get; set; } = new();
    public List<TrackItem> SubtitleTracks { get; set; } = new();
    public List<TrackItem> VideoTracks { get; set; } = new();

    public int SelectedAudioTrackId { get; set; } = -1;
    public int SelectedSubtitleTrackId { get; set; } = -1;
    public int SelectedVideoTrackId { get; set; } = -1;

    public uint VideoWidth { get; set; }
    public uint VideoHeight { get; set; }
    public string VideoCodec { get; set; } = string.Empty;
    public float FrameRate { get; set; }
    public string AudioCodec { get; set; } = string.Empty;
    public uint AudioChannels { get; set; }
    public uint AudioSampleRate { get; set; }
}

public class MediaDetails
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Container { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public uint Width { get; set; }
    public uint Height { get; set; }
    public string VideoCodec { get; set; } = string.Empty;
    public float FrameRate { get; set; }
    public string AudioCodec { get; set; } = string.Empty;
    public uint AudioChannels { get; set; }
    public uint AudioSampleRate { get; set; }
    public List<string> SubtitleTrackNames { get; set; } = new();
    public List<string> AudioTrackNames { get; set; } = new();
    public List<string> VideoTrackNames { get; set; } = new();

    public string ResolutionText => Width > 0 && Height > 0 ? $"{Width} × {Height}" : "Unknown";
    public string FrameRateText => FrameRate > 0 ? $"{FrameRate:0.##} fps" : "Unknown";
    public string AudioChannelsText => AudioChannels > 0 ? (AudioChannels == 1 ? "Mono (1.0)" : AudioChannels == 2 ? "Stereo (2.0)" : AudioChannels == 6 ? "5.1 Surround" : $"{AudioChannels} channels") : "Unknown";
    public string AudioSampleRateText => AudioSampleRate > 0 ? $"{AudioSampleRate} Hz" : "Unknown";
    public string SubtitlesSummary => SubtitleTrackNames.Count > 0 ? string.Join(", ", SubtitleTrackNames) : "None";
    public string AudioTracksSummary => AudioTrackNames.Count > 0 ? string.Join(", ", AudioTrackNames) : "Default";

    public string FormattedFileSize
    {
        get
        {
            if (FileSizeBytes <= 0) return "Unknown";
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = FileSizeBytes;
            int unitIndex = 0;
            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }
            return $"{size:0.##} {units[unitIndex]}";
        }
    }

    public string FormattedDuration
    {
        get
        {
            if (Duration.TotalHours >= 1)
                return $"{(int)Duration.TotalHours}:{Duration.Minutes:D2}:{Duration.Seconds:D2}";
            return $"{Duration.Minutes:D2}:{Duration.Seconds:D2}";
        }
    }
}
