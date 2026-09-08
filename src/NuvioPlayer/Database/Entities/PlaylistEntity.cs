using System.ComponentModel.DataAnnotations;

namespace NuvioPlayer.Database.Entities;

public class PlaylistEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<PlaylistItemEntity> Items { get; set; } = new();
}
