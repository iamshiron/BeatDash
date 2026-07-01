using Microsoft.AspNetCore.Identity;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.API.Seeders;

public static class AdminUserSeeder {
    public static async Task SeedAdminUser(this IServiceScope scope, string adminEmail, string adminPassword, string adminRole) {
        var services = scope.ServiceProvider;
        try {
            var context = services.GetRequiredService<BeatDashDbContext>();
            var userManager = services.GetRequiredService<UserManager<User>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var config = services.GetRequiredService<IConfiguration>();

            if (!await roleManager.RoleExistsAsync(adminRole)) {
                await roleManager.CreateAsync(new IdentityRole<Guid>(adminRole));
            }

            var existingAdmin = await userManager.FindByNameAsync("admin") ?? await userManager.FindByEmailAsync(adminEmail);
            if (existingAdmin == null) {
                var admin = new User {
                    DisplayName = "Admin",
                    Email = adminEmail,
                    UserName = "admin",
                    EmailConfirmed = true
                };

                var res = await userManager.CreateAsync(admin, adminPassword);
                if (res.Succeeded) {
                    await userManager.AddToRoleAsync(admin, adminRole);
                } else {
                    var errors = string.Join(", ", res.Errors.Select(e => e.Description));
                    throw new Exception($"Failed to create admin user: {errors}");
                }
            }
        } catch (Exception e) {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(e, "A fatal error occurred applying DB migrations or seeding the admin user.");
            throw;
        }
    }
}
