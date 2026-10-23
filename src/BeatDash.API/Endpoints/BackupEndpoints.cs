using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.API.Services.Backup;
using Shiron.BeatDash.Data.Backup;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.API.Endpoints;

/// <summary>Administrative backup API used by the CLI.</summary>
public static class BackupEndpoints {
    /// <summary>Maps authentication, export, restore, and interrupted-restore recovery.</summary>
    public static void MapBackupEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup("/admin/backups").WithTags("Admin backups");
        group.MapPost("/login", LoginAsync).AllowAnonymous();
        var secured = group.MapGroup("").RequireAuthorization("BackupAdmin");
        secured.AddEndpointFilter(async (context, next) => {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            try { return await next(context); } catch (InvalidDataException ex) { return Results.BadRequest(new BackupErrorResponse(ex.Message)); } catch (JsonException) { return Results.BadRequest(new BackupErrorResponse("Malformed backup manifest.")); } catch (InvalidOperationException ex) { return Results.Conflict(new BackupErrorResponse(ex.Message)); }
        });
        secured.MapPost("/export", async (BackupExportRequest request, BackupService service, CancellationToken ct) => {
            var path = await service.ExportAsync(request.Scope, ct);
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
                FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            return Results.File(stream, "application/zip", $"beatdash-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip");
        }).Produces(200, contentType: "application/zip");
        secured.MapPost("/restore", async ([AsParameters] BackupRestoreRequest request, HttpContext context, BackupService service,
            IOptions<BackupOptions> options, CancellationToken ct) => {
                var limit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
                if (limit is { IsReadOnly: false }) limit.MaxRequestBodySize = options.Value.MaxArchiveBytes;
                if (context.Request.ContentType != "application/zip") return Results.BadRequest(new BackupErrorResponse("Expected application/zip."));
                if (context.Request.ContentLength > options.Value.MaxArchiveBytes) return Results.StatusCode(413);
                return Results.Ok(await service.RestoreAsync(context.Request.Body, request.Scope, ct));
            }).Produces<BackupRestoreResponse>();
        secured.MapPost("/recover", async (BackupService service, CancellationToken ct) =>
            Results.Ok(await service.RecoverAsync(ct))).Produces<BackupRestoreResponse>();
    }

    private static async Task<IResult> LoginAsync(BackupLoginRequest request, UserManager<User> users,
        SignInManager<User> signIn, IConfiguration configuration, HttpContext context) {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password)) return Results.Unauthorized();
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null) return Results.Unauthorized();
        var check = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!check.Succeeded) return Results.Unauthorized();
        if (await users.GetTwoFactorEnabledAsync(user)) {
            if (string.IsNullOrWhiteSpace(request.TwoFactorCode)
                || !await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider,
                    request.TwoFactorCode.Replace(" ", "").Replace("-", ""))) {
                await users.AccessFailedAsync(user);
                return Results.Unauthorized();
            }
            await users.ResetAccessFailedCountAsync(user);
        }
        if (!await users.IsInRoleAsync(user, configuration["AdminUser:Role"] ?? "Admin")) return Results.Forbid();
        var expires = DateTimeOffset.UtcNow.AddHours(1);
        var token = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"], [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("purpose", "beatdash-backup"),
            new Claim("security_stamp", await users.GetSecurityStampAsync(user)),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        ], expires: expires.UtcDateTime, signingCredentials: new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SecretKey"]!)), SecurityAlgorithms.HmacSha256));
        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new BackupLoginResponse(new JwtSecurityTokenHandler().WriteToken(token), expires));
    }
}
