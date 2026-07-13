using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.API.Endpoints;

/// <summary>
/// User-created lists that group maps together (e.g. "Warmup", "Trying to Beat").
/// A user owns any number of lists; a map may belong to any number of them.
/// </summary>
public static class MapListEndpoints {
    public static void MapMapListEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup("/lists").WithTags("Lists");

        // All of the current user's lists, newest first. When mapId is supplied, each
        // list also reports whether it already contains that map (used by the
        // "add to list" picker).
        group.MapGet("/", async (
            Guid? mapId,
            ClaimsPrincipal user,
            BeatDashDbContext db,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();

                var lists = await db.MapLists
                    .AsNoTracking()
                    .Where(l => l.UserId == userId.Value)
                    .OrderByDescending(l => l.CreatedAt)
                    .Select(l => new {
                        l.Id,
                        l.Name,
                        l.Description,
                        l.CreatedAt,
                        l.UpdatedAt,
                        MapCount = l.Items.Count,
                        ContainsMap = mapId.HasValue && l.Items.Any(i => i.BeatmapId == mapId.Value),
                        // Beatmap ids of the first few maps with a cover, for a stacked preview.
                        CoverMapIds = l.Items
                            .OrderBy(i => i.Position)
                            .Where(i => i.Beatmap.CoverImageKey != null)
                            .Select(i => i.BeatmapId)
                            .Take(4)
                            .ToList(),
                    })
                    .ToListAsync(ct);

                var result = lists
                    .Select(l => new MapListSummaryDto(
                        l.Id, l.Name, l.Description, l.MapCount, l.CreatedAt, l.UpdatedAt,
                        mapId.HasValue ? l.ContainsMap : null,
                        l.CoverMapIds))
                    .ToList();

                return Results.Ok(result);
            }).RequireAuthorization().Produces<IList<MapListSummaryDto>>();

        // Create a new list.
        group.MapPost("/", async (
            [FromBody] CreateMapListDto body,
            ClaimsPrincipal user,
            BeatDashDbContext db,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();

                var name = body.Name?.Trim();
                if (string.IsNullOrEmpty(name)) return Results.BadRequest("A list name is required.");
                if (name.Length > 64) return Results.BadRequest("List name is too long (max 64).");

                var list = new MapList {
                    UserId = userId.Value,
                    Name = name,
                    Description = string.IsNullOrWhiteSpace(body.Description) ? null : body.Description.Trim(),
                };
                db.MapLists.Add(list);
                await db.SaveChangesAsync(ct);

                var dto = new MapListSummaryDto(
                    list.Id, list.Name, list.Description, 0, list.CreatedAt, list.UpdatedAt, null, []);
                return Results.Created($"/api/lists/{list.Id}", dto);
            }).RequireAuthorization().Produces<MapListSummaryDto>(201).Produces(400);

        // A list with its maps, ordered by position.
        group.MapGet("/{listId:Guid}", async (
            Guid listId,
            ClaimsPrincipal user,
            BeatDashDbContext db,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();

                var list = await db.MapLists
                    .AsNoTracking()
                    .FirstOrDefaultAsync(l => l.Id == listId && l.UserId == userId.Value, ct);
                if (list is null) return Results.NotFound();

                var listItems = await db.MapListItems
                    .AsNoTracking()
                    .Where(i => i.MapListId == listId)
                    .OrderBy(i => i.Position)
                    .Include(i => i.Beatmap).ThenInclude(b => b.Difficulties).ThenInclude(d => d.Analysis)
                    .Include(i => i.Beatmap).ThenInclude(b => b.BeatSaverMap)
                    .ToListAsync(ct);

                var maps = listItems.Select(i => i.Beatmap).ToList();
                var items = await BuildMapItemsAsync(db, userId.Value, maps, ct);
                return Results.Ok(new MapListDetailDto(
                    list.Id, list.Name, list.Description, list.CreatedAt, list.UpdatedAt, items));
            }).RequireAuthorization().Produces<MapListDetailDto>().Produces(404);

        // Rename / re-describe a list.
        group.MapPatch("/{listId:Guid}", async (
            Guid listId,
            [FromBody] UpdateMapListDto body,
            ClaimsPrincipal user,
            BeatDashDbContext db,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();

                var list = await db.MapLists
                    .FirstOrDefaultAsync(l => l.Id == listId && l.UserId == userId.Value, ct);
                if (list is null) return Results.NotFound();

                if (body.Name is not null) {
                    var name = body.Name.Trim();
                    if (string.IsNullOrEmpty(name)) return Results.BadRequest("A list name is required.");
                    if (name.Length > 64) return Results.BadRequest("List name is too long (max 64).");
                    list.Name = name;
                }
                if (body.Description is not null) {
                    list.Description = string.IsNullOrWhiteSpace(body.Description) ? null : body.Description.Trim();
                }
                list.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                return Results.NoContent();
            }).RequireAuthorization().Produces(204).Produces(400).Produces(404);

        // Delete a list (its memberships cascade).
        group.MapDelete("/{listId:Guid}", async (
            Guid listId,
            ClaimsPrincipal user,
            BeatDashDbContext db,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();

                var deleted = await db.MapLists
                    .Where(l => l.Id == listId && l.UserId == userId.Value)
                    .ExecuteDeleteAsync(ct);

                return deleted == 0 ? Results.NotFound() : Results.NoContent();
            }).RequireAuthorization().Produces(204).Produces(404);

        // Add a map to a list. Idempotent; appends at the end.
        group.MapPut("/{listId:Guid}/maps/{mapId:Guid}", async (
            Guid listId,
            Guid mapId,
            ClaimsPrincipal user,
            BeatDashDbContext db,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();

                var listOwned = await db.MapLists.AnyAsync(l => l.Id == listId && l.UserId == userId.Value, ct);
                if (!listOwned) return Results.NotFound();

                var mapExists = await db.Beatmaps.AnyAsync(b => b.Id == mapId, ct);
                if (!mapExists) return Results.NotFound();

                var already = await db.MapListItems.AnyAsync(i => i.MapListId == listId && i.BeatmapId == mapId, ct);
                if (!already) {
                    var maxPos = await db.MapListItems
                        .Where(i => i.MapListId == listId)
                        .Select(i => (int?) i.Position)
                        .MaxAsync(ct) ?? -1;
                    db.MapListItems.Add(new MapListItem {
                        MapListId = listId,
                        BeatmapId = mapId,
                        Position = maxPos + 1,
                    });
                    await db.MapLists
                        .Where(l => l.Id == listId)
                        .ExecuteUpdateAsync(s => s.SetProperty(l => l.UpdatedAt, DateTime.UtcNow), ct);
                    await db.SaveChangesAsync(ct);
                }

                return Results.NoContent();
            }).RequireAuthorization().Produces(204).Produces(404);

        // Remove a map from a list. Idempotent.
        group.MapDelete("/{listId:Guid}/maps/{mapId:Guid}", async (
            Guid listId,
            Guid mapId,
            ClaimsPrincipal user,
            BeatDashDbContext db,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();

                var listOwned = await db.MapLists.AnyAsync(l => l.Id == listId && l.UserId == userId.Value, ct);
                if (!listOwned) return Results.NotFound();

                await db.MapListItems
                    .Where(i => i.MapListId == listId && i.BeatmapId == mapId)
                    .ExecuteDeleteAsync(ct);
                await db.MapLists
                    .Where(l => l.Id == listId)
                    .ExecuteUpdateAsync(s => s.SetProperty(l => l.UpdatedAt, DateTime.UtcNow), ct);

                return Results.NoContent();
            }).RequireAuthorization().Produces(204).Produces(404);
    }

    /// <summary>
    /// Enriches a set of beatmaps into <see cref="MapListItemDto"/>s with the current
    /// user's play count and like state plus the total like count, preserving input order.
    /// Mirrors the per-page enrichment done by the maps browser.
    /// </summary>
    private static async Task<IList<MapListItemDto>> BuildMapItemsAsync(
        BeatDashDbContext db, Guid userId, IList<Beatmap> maps, CancellationToken ct) {
        if (maps.Count == 0) return [];
        var ids = maps.Select(m => m.Id).ToList();

        var playCounts = await db.PlaySessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && !s.AutoMode && ids.Contains(s.BeatmapDifficulty.BeatmapId))
            .GroupBy(s => s.BeatmapDifficulty.BeatmapId)
            .Select(g => new { BeatmapId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BeatmapId, x => x.Count, ct);

        var likedIds = (await db.MapLikes
            .AsNoTracking()
            .Where(l => l.UserId == userId && ids.Contains(l.BeatmapId))
            .Select(l => l.BeatmapId)
            .ToListAsync(ct)).ToHashSet();

        var likeCounts = await db.MapLikes
            .AsNoTracking()
            .Where(l => ids.Contains(l.BeatmapId))
            .GroupBy(l => l.BeatmapId)
            .Select(g => new { BeatmapId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BeatmapId, x => x.Count, ct);

        return maps
            .Select(m => MapListItemDto.From(
                m,
                playCounts.GetValueOrDefault(m.Id),
                likedIds.Contains(m.Id),
                likeCounts.GetValueOrDefault(m.Id)))
            .ToList();
    }
}

/// <summary>A user's list as it appears in the lists overview / picker.</summary>
public sealed record MapListSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    int MapCount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    // Only populated when the lists were queried in the context of a specific map.
    bool? ContainsMap,
    // Beatmap ids (up to four) for a stacked cover preview.
    IList<Guid> CoverMapIds
);

/// <summary>A list plus its ordered maps.</summary>
public sealed record MapListDetailDto(
    Guid Id,
    string Name,
    string? Description,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IList<MapListItemDto> Maps
);

public sealed record CreateMapListDto(string Name, string? Description);
public sealed record UpdateMapListDto(string? Name, string? Description);
