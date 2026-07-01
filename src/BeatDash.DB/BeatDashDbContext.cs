using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.DB;

public class BeatDashDbContext(DbContextOptions<BeatDashDbContext> options) : IdentityDbContext<User, IdentityRole<Guid>, Guid>(options) {
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Device> Devices => Set<Device>();

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
    }
}
