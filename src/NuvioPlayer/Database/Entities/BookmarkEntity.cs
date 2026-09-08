using System.ComponentModel.DataAnnotations;

namespace NuvioPlayer.Database.Entities;

public class BookmarkEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string FilePath { get; set; } = string.Empty;

    public long PositionMs { get; set; }

    public string Label { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string FormattedPosition
    {
        get
        {
            var span = TimeSpan.FromMilliseconds(PositionMs);
            if (span.TotalHours >= 1)
                return $"{(int)span.TotalHours}:{span.Minutes:D2}:{span.Seconds:D2}";
            return $"{span.Minutes:D2}:{span.Seconds:D2}";
        }
    }
}
