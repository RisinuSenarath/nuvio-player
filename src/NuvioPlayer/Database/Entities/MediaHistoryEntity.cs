using System.ComponentModel.DataAnnotations;

namespace NuvioPlayer.Database.Entities;

public class MediaHistoryEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string FilePath { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public long LastPositionMs { get; set; }

    public long DurationMs { get; set; }

    public DateTime LastPlayedAt { get; set; } = DateTime.UtcNow;

    public int PlayCount { get; set; } = 1;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string RelativeTime
    {
        get
        {
            var diff = DateTime.UtcNow - LastPlayedAt;
            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 2) return "Yesterday";
            if (diff.TotalDays < 30) return $"{(int)diff.TotalDays}d ago";
            return LastPlayedAt.ToLocalTime().ToString("MMM dd, yyyy");
        }
    }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string ProgressText
    {
        get
        {
            if (DurationMs <= 0) return "";
            var pos = TimeSpan.FromMilliseconds(LastPositionMs);
            var dur = TimeSpan.FromMilliseconds(DurationMs);
            string formatPos = pos.TotalHours >= 1 ? $"{(int)pos.TotalHours}:{pos.Minutes:D2}:{pos.Seconds:D2}" : $"{pos.Minutes:D2}:{pos.Seconds:D2}";
            string formatDur = dur.TotalHours >= 1 ? $"{(int)dur.TotalHours}:{dur.Minutes:D2}:{dur.Seconds:D2}" : $"{dur.Minutes:D2}:{dur.Seconds:D2}";
            return $"{formatPos} / {formatDur}";
        }
    }
}
