using Microsoft.AspNetCore.Identity;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.API.Seeders;

/// <summary>Creates the single local user of a desktop installation.</summary>
public static class DesktopUserSeeder {
    /// <summary>The local user's fixed user name; it has no password or email and cannot log in.</summary>
    public const string UserName = "player";

    /// <summary>Ensures the local user exists and administers its own installation.</summary>
    public static async Task SeedDesktopUser(this IServiceScope scope, string role) {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roleManager.RoleExistsAsync(role)) Check(await roleManager.CreateAsync(new IdentityRole<Guid>(role)));

        var user = await userManager.FindByNameAsync(UserName);
        if (user is null) {
            user = new User { DisplayName = "Player", UserName = UserName };
            Check(await userManager.CreateAsync(user));
        }
        if (!await userManager.IsInRoleAsync(user, role)) Check(await userManager.AddToRoleAsync(user, role));
    }

    private static void Check(IdentityResult result) {
        if (!result.Succeeded)
            throw new InvalidOperationException("Failed to create the desktop user: " + string.Join(", ", result.Errors.Select(e => e.Description)));
    }
}
