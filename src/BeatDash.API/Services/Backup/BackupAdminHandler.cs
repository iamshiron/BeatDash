using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.API.Services.Backup;

/// <summary>Rechecks role membership and security stamp on every backup request.</summary>
public sealed class BackupAdminHandler(UserManager<User> users, IConfiguration configuration)
    : AuthorizationHandler<BackupAdminRequirement> {
    /// <inheritdoc/>
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, BackupAdminRequirement requirement) {
        if (context.User.FindFirst("purpose")?.Value != "beatdash-backup") return;
        var user = await users.GetUserAsync(context.User);
        if (user is null || await users.IsLockedOutAsync(user)
            || !await users.IsInRoleAsync(user, configuration["AdminUser:Role"] ?? "Admin")
            || context.User.FindFirst("security_stamp")?.Value != await users.GetSecurityStampAsync(user)) return;
        context.Succeed(requirement);
    }
}
