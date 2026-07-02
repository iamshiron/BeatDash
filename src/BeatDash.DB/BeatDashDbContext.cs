using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.DB;

public class BeatDashDbContext(DbContextOptions<BeatDashDbContext> options) : IdentityDbContext<User, IdentityRole<Guid>, Guid>(options) {
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Beatmap> Beatmaps => Set<Beatmap>();
    public DbSet<BeatmapDifficulty> BeatmapDifficulties => Set<BeatmapDifficulty>();

    protected override void OnModelCreating(ModelBuilder builder) {
        base.OnModelCreating(builder);

        builder.Entity<User>(c => {
            c.HasMany(x => x.Devices)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            c.ToTable("Users");
        });

        builder.Entity<IdentityRole<Guid>>().ToTable("Roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");

        builder.Entity<Device>(c => {
            c.HasKey(x => x.Id);
            c.Property(x => x.Name).IsRequired();
            c.HasIndex(x => new { x.ClientId, x.UserId }).IsUnique();
            c.HasMany(x => x.RefreshTokens)
                .WithOne(x => x.Device)
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
            c.ToTable("Devices");
        });

        builder.Entity<RefreshToken>(c => {
            c.HasKey(x => x.Id);
            c.Property(x => x.Expires).IsRequired();
            c.HasIndex(x => x.Token).IsUnique();
            c.ToTable("RefreshTokens");
        });

        builder.Entity<Beatmap>(c => {
            c.HasKey(x => x.Id);
            c.HasIndex(x => x.LevelId).IsUnique();
            c.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.SubmittedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            c.HasMany(x => x.Difficulties)
                .WithOne(x => x.Beatmap)
                .HasForeignKey(x => x.BeatmapId)
                .OnDelete(DeleteBehavior.Cascade);
            c.ToTable("Beatmaps");
        });

        builder.Entity<BeatmapDifficulty>(c => {
            c.HasKey(x => x.Id);
            c.HasIndex(x => new { x.BeatmapId, x.CharacteristicSerializedName, x.DifficultyRank }).IsUnique();
            c.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.SubmittedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            c.HasOne(x => x.Beatmap)
                .WithMany(x => x.Difficulties)
                .HasForeignKey(x => x.BeatmapId)
                .OnDelete(DeleteBehavior.Cascade);
            c.ToTable("BeatmapDifficulties");
        });
    }
}
