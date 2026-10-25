using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.API.Seeders;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.API.Endpoints;

/// <summary>Signs the desktop shell's web view in as the local user; only mapped in desktop mode.</summary>
public static class DesktopEndpoints {
    /// <summary>Maps the session handoff the shell navigates its web view to on launch.</summary>
    public static void MapDesktopEndpoints(this IEndpointRouteBuilder endpoints) {
        endpoints.MapGet("/desktop/session", SignInAsync)
            .WithTags("Desktop")
            .ExcludeFromDescription();
    }

    private static async Task<IResult> SignInAsync(string? token, IOptions<DesktopOptions> options,
        UserManager<User> users, SignInManager<User> signIn) {
        if (!Matches(token, options.Value.SessionToken)) return Results.NotFound();
        var user = await users.FindByNameAsync(DesktopUserSeeder.UserName);
        if (user is null) return Results.NotFound();
        await signIn.SignInAsync(user, isPersistent: false);
        return Results.Redirect("/");
    }

    private static bool Matches(string? token, string expected) =>
        token is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(expected));
}
