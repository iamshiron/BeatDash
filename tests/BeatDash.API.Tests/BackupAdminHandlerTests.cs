using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shiron.BeatDash.API.Services.Backup;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;
using Xunit;

namespace Shiron.BeatDash.API.Tests;

public sealed class BackupAdminHandlerTests {
    [Fact]
    public async Task Authorize_CurrentAdminWithBackupToken_Succeeds() => Assert.True(await AuthorizeAsync());

    [Fact]
    public async Task Authorize_OrdinaryUserWithForgedRoleClaim_IsDenied() => Assert.False(await AuthorizeAsync(admin: false));

    [Fact]
    public async Task Authorize_RevokedSecurityStamp_IsDenied() => Assert.False(await AuthorizeAsync(stamp: "old"));

    [Fact]
    public async Task Authorize_OrdinaryDeviceToken_IsDenied() => Assert.False(await AuthorizeAsync(purpose: "device"));

    [Fact]
    public async Task Authorize_DeletedUser_IsDenied() => Assert.False(await AuthorizeAsync(exists: false));

    [Fact]
    public async Task Authorize_LockedOutAdministrator_IsDenied() => Assert.False(await AuthorizeAsync(lockedOut: true));

    private static async Task<bool> AuthorizeAsync(bool admin = true, string stamp = "current", string purpose = "beatdash-backup",
        bool exists = true, bool lockedOut = false) {
        using var db = new BeatDashDbContext(new DbContextOptionsBuilder<BeatDashDbContext>().UseNpgsql("Host=unused").Options);
        using var users = new TestUserManager(db) { Admin = admin, Exists = exists, LockedOut = lockedOut };
        var handler = new BackupAdminHandler(users, new ConfigurationBuilder().Build());
        var requirement = new BackupAdminRequirement();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "Admin"), new Claim("purpose", purpose), new Claim("security_stamp", stamp)
        ], "Bearer"));
        var context = new AuthorizationHandlerContext([requirement], principal, null);
        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    private sealed class TestUserManager(BeatDashDbContext db) : UserManager<User>(
        new UserStore<User, IdentityRole<Guid>, BeatDashDbContext, Guid>(db), Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
        new PasswordHasher<User>(), [], [], new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(),
        new ServiceCollection().BuildServiceProvider(), NullLogger<UserManager<User>>.Instance) {
        public bool Admin { get; init; }
        public bool Exists { get; init; }
        public bool LockedOut { get; init; }
        public override Task<User?> GetUserAsync(ClaimsPrincipal principal) => Task.FromResult(Exists ? new User { DisplayName = "Admin" } : null);
        public override Task<bool> IsInRoleAsync(User user, string role) => Task.FromResult(Admin);
        public override Task<bool> IsLockedOutAsync(User user) => Task.FromResult(LockedOut);
        public override Task<string> GetSecurityStampAsync(User user) => Task.FromResult("current");
    }
}
