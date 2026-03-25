using Microsoft.EntityFrameworkCore;
using Shiron.BeatDash.API.Data.Entities;

namespace Shiron.BeatDash.API.Data;

public class BeatDashDbContext : DbContext {
    public DbSet<PlaySessionEntity> PlaySessions { get; set; }
    public DbSet<LiveDataSnapshotEntity> LiveDataSnapshots { get; set; }
    public DbSet<RawMessageEntity> RawMessages { get; set; }
    public DbSet<ModifiersEntity> Modifiers { get; set; }
    public DbSet<PracticeModeModifiersEntity> PracticeModeModifiers { get; set; }

    public BeatDashDbContext(DbContextOptions<BeatDashDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.Entity<PlaySessionEntity>(entity => {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.StartedAt);
            entity.HasIndex(e => e.Hash);
            entity.HasIndex(e => e.SongName);
            entity.HasIndex(e => e.SongAuthor);
            entity.HasIndex(e => e.Mapper);
            entity.HasIndex(e => e.BSRKey);
            entity.HasIndex(e => e.Difficulty);
            entity.HasIndex(e => e.FinishedAt);

            entity.Property(e => e.SongName).HasMaxLength(256);
            entity.Property(e => e.SongSubName).HasMaxLength(256);
            entity.Property(e => e.SongAuthor).HasMaxLength(256);
            entity.Property(e => e.Mapper).HasMaxLength(256);
            entity.Property(e => e.Hash).HasMaxLength(64);
            entity.Property(e => e.BSRKey).HasMaxLength(64);
            entity.Property(e => e.MapType).HasMaxLength(64);
            entity.Property(e => e.Difficulty).HasMaxLength(64);
            entity.Property(e => e.CustomDifficultyLabel).HasMaxLength(64);
            entity.Property(e => e.GameVersion).HasMaxLength(32);
            entity.Property(e => e.PluginVersion).HasMaxLength(32);
            entity.Property(e => e.EndReason).HasMaxLength(16);
            entity.Property(e => e.FinalRank).HasMaxLength(8);

            entity.HasOne(e => e.Modifiers)
                .WithOne()
                .HasForeignKey<PlaySessionEntity>("ModifiersId");

            entity.HasOne(e => e.PracticeModeModifiers)
                .WithOne()
                .HasForeignKey<PlaySessionEntity>("PracticeModeModifiersId");
        });

        modelBuilder.Entity<LiveDataSnapshotEntity>(entity => {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.PlaySessionId);
            entity.HasIndex(e => e.EventTrigger);

            entity.Property(e => e.Rank).HasMaxLength(8);

            entity.HasOne(e => e.PlaySession)
                .WithMany(p => p.LiveDataSnapshots)
                .HasForeignKey(e => e.PlaySessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RawMessageEntity>(entity => {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.ConnectionName);
            entity.HasIndex(e => e.PlaySessionId);

            entity.Property(e => e.ConnectionName).HasMaxLength(32);

            entity.HasOne(e => e.PlaySession)
                .WithMany()
                .HasForeignKey(e => e.PlaySessionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ModifiersEntity>(entity => {
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<PracticeModeModifiersEntity>(entity => {
            entity.HasKey(e => e.Id);
        });
    }
}
