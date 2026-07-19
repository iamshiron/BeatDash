using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shiron.BeatDash.Data.Socket;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.API.Services;
using Shiron.BeatDash.Data;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.API.Endpoints;

public static class IdentityEndpoints {
    public static void MapIdentityEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", Register)
            .WithName("Register")
            .WithDescription("Register a new user account")
            .Produces(200)
            .ProducesValidationProblem();

        group.MapPost("/login", Login)
            .WithName("Login")
            .WithDescription("Log in with credentials")
            .Produces(200)
            .Produces(401);

        group.MapPost("/logout", Logout)
            .WithName("Logout")
            .WithDescription("Log out the current session")
            .RequireAuthorization()
            .Produces(200);

        group.MapGet("/me", Me)
            .WithName("GetMe")
            .WithDescription("Get the current authenticated user info")
            .RequireAuthorization()
            .Produces<UserInfoDto>()
            .Produces(401);

        group.MapPut("/me", UpdateProfile)
            .WithName("UpdateProfile")
            .WithDescription("Update the current user's profile")
            .RequireAuthorization()
            .Produces<UserInfoDto>()
            .Produces(400)
            .Produces(401);

        group.MapPut("/me/avatar", UploadAvatar)
            .WithName("UploadAvatar")
            .WithDescription("Upload the current user's avatar image")
            .RequireAuthorization()
            .DisableAntiforgery()
            .Produces<UserInfoDto>()
            .Produces(400)
            .Produces(401);

        group.MapPut("/me/banner", UploadBanner)
            .WithName("UploadBanner")
            .WithDescription("Upload the current user's profile banner image")
            .RequireAuthorization()
            .DisableAntiforgery()
            .Produces<UserInfoDto>()
            .Produces(400)
            .Produces(401);

        group.MapPost("/change-password", ChangePassword)
            .WithName("ChangePassword")
            .WithDescription("Change the current user's password")
            .RequireAuthorization()
            .Produces(200)
            .Produces(400);

        group.MapPost("/refresh-token", RefreshToken)
            .WithName("RefreshToken")
            .WithDescription("Refresh the current user's access token")
            .AllowAnonymous()
            .Produces<TokenPairExpiryDto>()
            .Produces(401);
    }

    private static async Task<IResult> Register(
        RegisterDto dto,
        UserManager<User> userManager) {
        var user = new User {
            DisplayName = dto.DisplayName,
            UserName = dto.UserName,
            Email = dto.Email,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return Results.BadRequest(result.Errors.Select(e => e.Description).ToList());

        return Results.Ok(new { Message = "User created successfully" });
    }

    private static async Task<IResult> Login(
        LoginDto dto,
        SignInManager<User> signInManager,
        UserManager<User> userManager) {
        var user = await userManager.FindByEmailAsync(dto.Email);
        if (user == null) return Results.Unauthorized();

        var result = await signInManager.PasswordSignInAsync(user, dto.Password, dto.RememberMe, false);
        if (!result.Succeeded) return Results.Unauthorized();

        return Results.Ok(new { Message = "Logged in successfully" });
    }

    private static async Task<IResult> Logout(SignInManager<User> signInManager) {
        await signInManager.SignOutAsync();
        return Results.Ok(new { Message = "Logged out successfully" });
    }

    private static async Task<IResult> Me(ClaimsPrincipal principal, UserManager<User> userManager) {
        var user = await userManager.GetUserAsync(principal);
        if (user == null) return Results.Unauthorized();

        var roles = await userManager.GetRolesAsync(user);
        return Results.Ok(ToUserInfo(user, roles));
    }

    private static async Task<IResult> UpdateProfile(
        UpdateProfileDto dto,
        ClaimsPrincipal principal,
        UserManager<User> userManager) {
        var user = await userManager.GetUserAsync(principal);
        if (user == null) return Results.Unauthorized();

        var displayName = dto.DisplayName.Trim();
        if (displayName.Length is 0 or > 32)
            return Results.BadRequest(new[] { "Display name must be between 1 and 32 characters." });

        // A blank handle leaves the existing one untouched; otherwise validate + ensure uniqueness.
        var normalizedHandle = HandleUtils.Normalize(dto.Handle);
        if (normalizedHandle is not null && normalizedHandle != user.Handle) {
            if (!HandleUtils.IsValid(normalizedHandle))
                return Results.BadRequest(new[] { "Handle must be 3–32 characters, lowercase letters, numbers or underscores." });

            var taken = await userManager.Users
                .AnyAsync(u => u.Handle == normalizedHandle && u.Id != user.Id);
            if (taken)
                return Results.BadRequest(new[] { "That handle is already taken." });

            user.Handle = normalizedHandle;
        }

        user.DisplayName = displayName;
        user.ProfileStatsPublic = dto.ProfileStatsPublic;
        user.ProfileActivityPublic = dto.ProfileActivityPublic;
        user.ProfileSkillPublic = dto.ProfileSkillPublic;
        user.ProfileHistoryPublic = dto.ProfileHistoryPublic;
        user.ProfileListsPublic = dto.ProfileListsPublic;
        user.ProfileLikedPublic = dto.ProfileLikedPublic;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return Results.BadRequest(result.Errors.Select(e => e.Description).ToList());

        var roles = await userManager.GetRolesAsync(user);
        return Results.Ok(ToUserInfo(user, roles));
    }

    private static Task<IResult> UploadAvatar(
        IFormFile file, ClaimsPrincipal principal, UserManager<User> userManager,
        IStorageService storage, IOptions<StorageOptions> options, CancellationToken ct)
        => UploadAsset(file, banner: false, principal, userManager, storage, options, ct);

    private static Task<IResult> UploadBanner(
        IFormFile file, ClaimsPrincipal principal, UserManager<User> userManager,
        IStorageService storage, IOptions<StorageOptions> options, CancellationToken ct)
        => UploadAsset(file, banner: true, principal, userManager, storage, options, ct);

    private static async Task<IResult> UploadAsset(
        IFormFile file, bool banner, ClaimsPrincipal principal, UserManager<User> userManager,
        IStorageService storage, IOptions<StorageOptions> options, CancellationToken ct) {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        if (file is null || file.Length == 0)
            return Results.BadRequest(new[] { "No image was uploaded." });
        if (file.Length > UserAssetUtils.MaxImageBytes)
            return Results.BadRequest(new[] { "Image is too large (max 5 MB)." });

        var ext = UserAssetUtils.ExtForContentType(file.ContentType);
        if (ext is null)
            return Results.BadRequest(new[] { "Unsupported image type. Use PNG, JPEG, WebP or GIF." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        var key = $"{(banner ? "banners" : "avatars")}/{user.Id}.{ext}";
        await storage.UploadAsync(options.Value.BucketUserData, key, file.ContentType, bytes, ct);

        if (banner) user.BannerKey = key; else user.AvatarKey = key;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return Results.BadRequest(result.Errors.Select(e => e.Description).ToList());

        var roles = await userManager.GetRolesAsync(user);
        return Results.Ok(ToUserInfo(user, roles));
    }

    private static UserInfoDto ToUserInfo(User user, IList<string> roles) => new() {
        ID = user.Id,
        DisplayName = user.DisplayName,
        UserName = user.UserName!,
        Email = user.Email!,
        Roles = roles.ToList(),
        Handle = user.Handle,
        ProfileStatsPublic = user.ProfileStatsPublic,
        ProfileActivityPublic = user.ProfileActivityPublic,
        ProfileSkillPublic = user.ProfileSkillPublic,
        ProfileHistoryPublic = user.ProfileHistoryPublic,
        ProfileListsPublic = user.ProfileListsPublic,
        ProfileLikedPublic = user.ProfileLikedPublic,
        AvatarUrl = user.AvatarKey is null ? null : $"/api/users/{user.Id}/avatar",
        BannerUrl = user.BannerKey is null ? null : $"/api/users/{user.Id}/banner"
    };

    private static async Task<IResult> ChangePassword(
        ChangePasswordDto dto,
        ClaimsPrincipal principal,
        UserManager<User> userManager) {
        var user = await userManager.GetUserAsync(principal);
        if (user == null) return Results.Unauthorized();

        var result = await userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
        if (!result.Succeeded)
            return Results.BadRequest(result.Errors.Select(e => e.Description).ToList());

        return Results.Ok(new { Message = "Password changed successfully" });
    }

    private static async Task<IResult> RefreshToken(ITokenService tokenService, BeatDashDbContext db, [FromBody] RefreshTokenRequestDto body) {
        var principal = tokenService.FromExpiredToken(body.AccessToken);
        if (principal == null) {
            return Results.Unauthorized();
        }

        var userId = IdentityUtils.GetUserID(principal);
        if (userId == null) {
            return Results.Unauthorized();
        }

        var existingToken = await db.RefreshTokens
            .Include(t => t.Device)
            .SingleOrDefaultAsync(t => t.Token == body.RefreshToken);

        if (existingToken == null
            || existingToken.Device.ClientId != body.ClientId
            || existingToken.Device.UserId != userId
            || existingToken.RevokedAt != null
            || existingToken.Expires < DateTime.UtcNow) {
            return Results.Unauthorized();
        }

        var newAccessToken = tokenService.GenerateAccessToken(userId.Value, out var newAccessTokenExpire);
        var newRefreshToken = tokenService.GenerateRefreshToken(out var newRefreshTokenExpire);
        existingToken.RevokedAt = DateTime.UtcNow;

        db.RefreshTokens.Add(new RefreshToken {
            Token = newRefreshToken,
            Expires = newRefreshTokenExpire,
            DeviceId = existingToken.DeviceId
        });

        await db.SaveChangesAsync();
        return Results.Ok(new TokenPairExpiryDto {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            AccessExpiresAt = newAccessTokenExpire,
            RefreshExpiresAt = newRefreshTokenExpire
        });
    }
}

public record RegisterDto {
    [Required][MaxLength(32)] public required string DisplayName { get; init; }
    [Required][MaxLength(32)] public required string UserName { get; init; }
    [Required][EmailAddress] public required string Email { get; init; }
    [Required][MinLength(4)] public required string Password { get; init; }
}

public record LoginDto {
    [Required][EmailAddress] public required string Email { get; init; }
    [Required] public required string Password { get; init; }
    public bool RememberMe { get; init; }
}

public record UpdateProfileDto {
    [Required][MaxLength(32)] public required string DisplayName { get; init; }

    /// <summary>New public handle. When null/blank the existing handle is left unchanged.</summary>
    [MaxLength(32)] public string? Handle { get; init; }

    public bool ProfileStatsPublic { get; init; }
    public bool ProfileActivityPublic { get; init; }
    public bool ProfileSkillPublic { get; init; }
    public bool ProfileHistoryPublic { get; init; }
    public bool ProfileListsPublic { get; init; }
    public bool ProfileLikedPublic { get; init; }
}

public record ChangePasswordDto {
    [Required] public required string CurrentPassword { get; init; }
    [Required][MinLength(4)] public required string NewPassword { get; init; }
}

public record UserInfoDto {
    public Guid ID { get; init; }
    public string DisplayName { get; init; } = default!;
    public string UserName { get; init; } = default!;
    public string Email { get; init; } = default!;
    public List<string> Roles { get; init; } = [];
    public string? Handle { get; init; }
    public bool ProfileStatsPublic { get; init; }
    public bool ProfileActivityPublic { get; init; }
    public bool ProfileSkillPublic { get; init; }
    public bool ProfileHistoryPublic { get; init; }
    public bool ProfileListsPublic { get; init; }
    public bool ProfileLikedPublic { get; init; }
    public string? AvatarUrl { get; init; }
    public string? BannerUrl { get; init; }
}
