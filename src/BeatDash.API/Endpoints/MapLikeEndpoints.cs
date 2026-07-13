using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.API.Endpoints;

/// <summary>
/// Like / unlike a whole map. The liked set is surfaced on the maps browser (via the
/// <c>Liked</c> filter and per-item <c>IsLiked</c> flag in <see cref="MapEndpoints"/>).
/// </summary>
public static class MapLikeEndpoints {
    public static void MapMapLikeEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup("/maps").WithTags("Maps");

        // Like a map. Idempotent: liking an already-liked map is a no-op success.
        group.MapPut("/{mapId:Guid}/like", async (
            Guid mapId,
            ClaimsPrincipal user,
            BeatDashDbContext db,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();

                var mapExists = await db.Beatmaps.AnyAsync(b => b.Id == mapId, ct);
                if (!mapExists) return Results.NotFound();

                var alreadyLiked = await db.MapLikes
                    .AnyAsync(l => l.UserId == userId.Value && l.BeatmapId == mapId, ct);
                if (!alreadyLiked) {
                    db.MapLikes.Add(new MapLike { UserId = userId.Value, BeatmapId = mapId });
                    await db.SaveChangesAsync(ct);
                }

                return Results.NoContent();
            }).RequireAuthorization().Produces(204).Produces(404);

        // Remove a like. Idempotent: unliking a map that isn't liked still succeeds.
        group.MapDelete("/{mapId:Guid}/like", async (
            Guid mapId,
            ClaimsPrincipal user,
            BeatDashDbContext db,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();

                await db.MapLikes
                    .Where(l => l.UserId == userId.Value && l.BeatmapId == mapId)
                    .ExecuteDeleteAsync(ct);

                return Results.NoContent();
            }).RequireAuthorization().Produces(204);
    }
}
