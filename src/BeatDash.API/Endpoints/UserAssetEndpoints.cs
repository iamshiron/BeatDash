using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.API.Services;
using Shiron.BeatDash.DB;

namespace Shiron.BeatDash.API.Endpoints;

/// <summary>
/// Public serving of user-uploaded profile images (avatar, banner). Bytes are
/// proxied from object storage, mirroring how map covers are served.
/// </summary>
public static class UserAssetEndpoints {
    public static void MapUserAssetEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup("/users").WithTags("UserAssets");

        group.MapGet("/{userId:guid}/avatar", (Guid userId, HttpContext http, BeatDashDbContext db, IStorageService storage, IOptions<StorageOptions> options, CancellationToken ct) =>
                ServeAsset(userId, banner: false, http, db, storage, options, ct))
            .WithName("GetUserAvatar")
            .WithDescription("Serve a user's uploaded avatar image.")
            .AllowAnonymous()
            .Produces(200)
            .Produces(404);

        group.MapGet("/{userId:guid}/banner", (Guid userId, HttpContext http, BeatDashDbContext db, IStorageService storage, IOptions<StorageOptions> options, CancellationToken ct) =>
                ServeAsset(userId, banner: true, http, db, storage, options, ct))
            .WithName("GetUserBanner")
            .WithDescription("Serve a user's uploaded profile banner image.")
            .AllowAnonymous()
            .Produces(200)
            .Produces(404);
    }

    private static async Task<IResult> ServeAsset(
        Guid userId,
        bool banner,
        HttpContext http,
        BeatDashDbContext db,
        IStorageService storage,
        IOptions<StorageOptions> options,
        CancellationToken ct) {
        var keys = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.AvatarKey, u.BannerKey })
            .FirstOrDefaultAsync(ct);

        var key = banner ? keys?.BannerKey : keys?.AvatarKey;
        if (string.IsNullOrEmpty(key)) return Results.NotFound();

        var data = await storage.DownloadAsync(options.Value.BucketUserData, key, ct);
        if (data is null) return Results.NotFound();

        // The object key is stable per user, so revalidate to surface fresh uploads.
        http.Response.Headers[HeaderNames.CacheControl] = "no-cache";
        return Results.File(data, UserAssetUtils.ContentTypeForKey(key));
    }
}

/// <summary>Maps between image content types and the file extension stored in object keys.</summary>
public static class UserAssetUtils {
    /// <summary>Largest accepted upload for a profile image, in bytes.</summary>
    public const long MaxImageBytes = 5 * 1024 * 1024;

    /// <summary>The stored extension for a supported image content type, or null if unsupported.</summary>
    public static string? ExtForContentType(string? contentType) => contentType?.ToLowerInvariant() switch {
        "image/png" => "png",
        "image/jpeg" => "jpg",
        "image/webp" => "webp",
        "image/gif" => "gif",
        _ => null
    };

    /// <summary>The content type to serve for a stored object key, inferred from its extension.</summary>
    public static string ContentTypeForKey(string key) => key.Split('.').LastOrDefault()?.ToLowerInvariant() switch {
        "jpg" or "jpeg" => "image/jpeg",
        "webp" => "image/webp",
        "gif" => "image/gif",
        _ => "image/png"
    };
}
