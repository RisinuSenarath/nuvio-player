using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NuvioPlayer.Database.Entities;

public class PlaylistItemEntity
{
    [Key]
    public int Id { get; set; }

    public int PlaylistId { get; set; }

    [Required]
    public string FilePath { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    [ForeignKey(nameof(PlaylistId))]
    public PlaylistEntity? Playlist { get; set; }
}
