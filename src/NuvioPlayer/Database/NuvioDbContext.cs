using Microsoft.EntityFrameworkCore;
using NuvioPlayer.Database.Entities;

namespace NuvioPlayer.Database;

public class NuvioDbContext : DbContext
{
    public DbSet<MediaHistoryEntity> MediaHistories => Set<MediaHistoryEntity>();
    public DbSet<PlaylistEntity> Playlists => Set<PlaylistEntity>();
    public DbSet<PlaylistItemEntity> PlaylistItems => Set<PlaylistItemEntity>();
    public DbSet<BookmarkEntity> Bookmarks => Set<BookmarkEntity>();
    public DbSet<SettingEntity> Settings => Set<SettingEntity>();

    public NuvioDbContext(DbContextOptions<NuvioDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // MediaHistory
        modelBuilder.Entity<MediaHistoryEntity>(entity =>
        {
            entity.HasIndex(e => e.FilePath).IsUnique();
            entity.HasIndex(e => e.LastPlayedAt);
        });

        // Playlists
        modelBuilder.Entity<PlaylistEntity>(entity =>
        {
            entity.HasMany(p => p.Items)
                  .WithOne(i => i.Playlist)
                  .HasForeignKey(i => i.PlaylistId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Bookmarks
        modelBuilder.Entity<BookmarkEntity>(entity =>
        {
            entity.HasIndex(b => b.FilePath);
        });
    }
}
