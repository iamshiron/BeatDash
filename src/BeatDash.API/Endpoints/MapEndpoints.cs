using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.API.Services;
using Shiron.BeatDash.API.Services.BeatSaver;
using Shiron.BeatDash.API.Services.Socket;
using Shiron.BeatDash.Data.Socket;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;
using Shiron.BeatDash.DB.Schema.BeatSaver;

namespace Shiron.BeatDash.API.Endpoints;

public static class MapEndpoints {
    private static IOrderedQueryable<Beatmap> Dir<TKey>(
        IQueryable<Beatmap> src, System.Linq.Expressions.Expression<Func<Beatmap, TKey>> key, bool asc)
        => asc ? src.OrderBy(key) : src.OrderByDescending(key);

    public static void MapMapEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup("/maps").WithTags("Maps");

        group.MapGet("/", async (
            [AsParameters] MapQueryParams q,
            BeatDashDbContext db,
            CancellationToken ct) => {
                var page = Math.Max(1, q.Page);
                var pageSize = Math.Clamp(q.PageSize, 1, 100);

                IQueryable<Beatmap> query = db.Beatmaps.AsNoTracking();

                // --- Text search ---
                if (!string.IsNullOrWhiteSpace(q.Search)) {
                    var pattern = $"%{q.Search.Trim()}%";
                    query = query.Where(b =>
                        EF.Functions.ILike(b.SongName, pattern) ||
                        EF.Functions.ILike(b.SongAuthor, pattern) ||
                        EF.Functions.ILike(b.Mapper, pattern) ||
                        (b.SongSubName != null && EF.Functions.ILike(b.SongSubName, pattern)));
                }

                // --- Per-difficulty filters: the map must have ONE difficulty matching all of them ---
                if (q.Characteristic != null || q.Difficulty != null || q.MinNps != null || q.MaxNps != null
                    || q.MinDifficulty != null || q.MaxDifficulty != null || q.MinPp != null || q.MaxPp != null) {
                    query = query.Where(b => b.Difficulties.Any(d =>
                        (q.Characteristic == null || d.CharacteristicSerializedName == q.Characteristic) &&
                        (q.Difficulty == null || d.DifficultyRank == q.Difficulty) &&
                        (q.MinNps == null || d.NotesPerSecond >= q.MinNps) &&
                        (q.MaxNps == null || d.NotesPerSecond <= q.MaxNps) &&
                        (q.MinDifficulty == null || (d.Analysis != null && d.Analysis.DifficultyRating >= q.MinDifficulty)) &&
                        (q.MaxDifficulty == null || (d.Analysis != null && d.Analysis.DifficultyRating <= q.MaxDifficulty)) &&
                        (q.MinPp == null || (d.Analysis != null && d.Analysis.Pp >= q.MinPp)) &&
                        (q.MaxPp == null || (d.Analysis != null && d.Analysis.Pp <= q.MaxPp))));
                }

                // --- Map-level filters ---
                if (q.MinBpm.HasValue) query = query.Where(b => b.Bpm >= q.MinBpm.Value);
                if (q.MaxBpm.HasValue) query = query.Where(b => b.Bpm <= q.MaxBpm.Value);
                if (q.MinDurationSeconds.HasValue) query = query.Where(b => b.DurationMs >= q.MinDurationSeconds.Value * 1000);
                if (q.MaxDurationSeconds.HasValue) query = query.Where(b => b.DurationMs <= q.MaxDurationSeconds.Value * 1000);
                if (q.FetchStatus.HasValue) query = query.Where(b => b.FetchStatus == q.FetchStatus.Value);

                if (q.Ranked.HasValue) {
                    query = q.Ranked.Value
                        ? query.Where(b => b.BeatSaverMap != null && (b.BeatSaverMap.Ranked || b.BeatSaverMap.BlRanked))
                        : query.Where(b => b.BeatSaverMap == null || (!b.BeatSaverMap.Ranked && !b.BeatSaverMap.BlRanked));
                }
                if (q.Automapper.HasValue) {
                    query = q.Automapper.Value
                        ? query.Where(b => b.BeatSaverMap != null && b.BeatSaverMap.Automapper)
                        : query.Where(b => b.BeatSaverMap == null || !b.BeatSaverMap.Automapper);
                }
                if (!string.IsNullOrWhiteSpace(q.Tag)) {
                    var tag = q.Tag.Trim();
                    query = query.Where(b => b.BeatSaverMap != null && b.BeatSaverMap.Tags.Contains(tag));
                }

                // --- Sorting. Nullable keys (metrics, BeatSaver stats) are coalesced to a
                // direction-appropriate sentinel so unanalyzed / un-fetched maps always sort
                // LAST, not first (Postgres puts NULLs first on DESC). Stable Id tiebreaker
                // because CreatedAt collides on bulk import. ---
                var asc = q.SortDir == SortDirection.Asc;
                var nf = asc ? float.MaxValue : float.MinValue;
                var nd = asc ? double.MaxValue : double.MinValue;
                var ni = asc ? int.MaxValue : int.MinValue;
                var ndt = asc ? DateTime.MaxValue : DateTime.MinValue;
                IOrderedQueryable<Beatmap> ordered = q.SortBy switch {
                    MapSortBy.SongName => Dir(query, b => b.SongName, asc),
                    MapSortBy.Bpm => Dir(query, b => b.Bpm, asc),
                    MapSortBy.Duration => Dir(query, b => b.DurationMs, asc),
                    MapSortBy.Nps => Dir(query, b => b.Difficulties.Max(d => (float?) d.NotesPerSecond) ?? nf, asc),
                    MapSortBy.Difficulty => Dir(query, b => b.Difficulties.Max(d => d.Analysis!.DifficultyRating) ?? nd, asc),
                    MapSortBy.Pp => Dir(query, b => b.Difficulties.Max(d => d.Analysis!.Pp) ?? nd, asc),
                    MapSortBy.Downloads => Dir(query, b => ((int?) b.BeatSaverMap!.Stats.Downloads) ?? ni, asc),
                    MapSortBy.Upvotes => Dir(query, b => ((int?) b.BeatSaverMap!.Stats.Upvotes) ?? ni, asc),
                    MapSortBy.Score => Dir(query, b => ((float?) b.BeatSaverMap!.Stats.Score) ?? nf, asc),
                    MapSortBy.Uploaded => Dir(query, b => b.BeatSaverMap!.Uploaded ?? ndt, asc),
                    _ => Dir(query, b => b.CreatedAt, asc),
                };
                query = ordered.ThenBy(b => b.Id);

                var totalCount = await query.CountAsync(ct);
                var totalPages = totalCount == 0 ? 0 : (int) Math.Ceiling(totalCount / (double) pageSize);

                var maps = await query
                    .Include(b => b.Difficulties).ThenInclude(d => d.Analysis)
                    .Include(b => b.BeatSaverMap)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(ct);

                var items = maps.Select(MapListItemDto.From).ToList();
                return Results.Ok(new PagedResult<MapListItemDto>(items, totalCount, page, pageSize, totalPages));
            })
            .RequireAuthorization()
            .Produces<PagedResult<MapListItemDto>>();

        group.MapGet("/{mapId:Guid}", async (
            Guid mapId,
            BeatDashDbContext db,
            CancellationToken ct) => {
                var map = await db.Beatmaps
                    .AsNoTracking()
                    .Include(b => b.Difficulties)
                    .FirstOrDefaultAsync(b => b.Id == mapId, ct);

                if (map is null) return Results.NotFound();
                return Results.Ok(MapDetailDto.From(map));
            }).RequireAuthorization().Produces<MapDetailDto>().Produces(404);

        group.MapGet("/{mapId:Guid}/cover", async (
            Guid mapId,
            BeatDashDbContext db,
            IStorageService storage,
            IOptions<StorageOptions> options,
            CancellationToken ct) => {
                var map = await db.Beatmaps
                    .AsNoTracking()
                    .Select(b => new { b.Id, b.CoverImageKey })
                    .FirstOrDefaultAsync(b => b.Id == mapId, ct);

                if (map is null || string.IsNullOrEmpty(map.CoverImageKey)) return Results.NotFound();

                var data = await storage.DownloadAsync(options.Value.BucketAssets, map.CoverImageKey, ct);
                if (data is null) return Results.NotFound();

                return Results.File(data, "image/png");
            }).RequireAuthorization().Produces(404).Produces(200);

        // Admin-only: force a map's BeatSaver data to be re-fetched and re-downloaded.
        group.MapPost("/{mapId:Guid}/refetch", async (
            Guid mapId,
            BeatDashDbContext db,
            IBeatSaverFetchTrigger trigger,
            CancellationToken ct) => {
                var beatmap = await db.Beatmaps.FirstOrDefaultAsync(b => b.Id == mapId, ct);
                if (beatmap is null) return Results.NotFound();

                beatmap.FetchStatus = BeatSaverFetchStatus.Pending;
                beatmap.FetchAttemptCount = 0;
                beatmap.FetchError = null;
                await db.SaveChangesAsync(ct);

                await trigger.TriggerMapAsync(mapId, force: true, ct);
                return Results.Accepted($"/api/maps/{mapId}");
            }).RequireAuthorization(p => p.RequireRole("Admin")).Produces(202).Produces(404);

        // Admin-only: import a map straight from parsed files (the persistence half of a
        // map-start, without a play session). Used by the `beatmap push` CLI to bulk-load
        // a CustomLevels directory. One call per (map, difficulty); the beatmap is deduped
        // by LevelId and the BeatSaver fetch fires on first sight.
        group.MapPost("/import", async (
            HttpContext http,
            [FromForm] string metadata,
            IFormFile cover,
            IBeatmapPersistenceService persistence,
            IBeatSaverFetchTrigger trigger,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(http.User);
                if (userId is null) return Results.Unauthorized();

                MapStartMessage? message;
                try {
                    message = JsonSerializer.Deserialize<MapStartMessage>(
                        metadata, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                } catch (JsonException) {
                    return Results.BadRequest("Invalid metadata JSON.");
                }
                if (message is null) return Results.BadRequest("Missing metadata.");

                using var ms = new MemoryStream();
                await cover.CopyToAsync(ms, ct);
                var coverBytes = ms.ToArray();
                if (coverBytes.Length == 0) return Results.BadRequest("Empty cover image.");

                var pair = new MapDataPair(message, coverBytes, userId.Value, Guid.Empty);
                var result = await persistence.PersistAsync(pair, ct);
                if (result.IsNew) await trigger.TriggerMapAsync(result.Id, force: false, ct);

                return Results.Ok(new MapImportResultDto(result.Id, result.IsNew));
            })
            .RequireAuthorization(p => p.RequireRole("Admin"))
            .DisableAntiforgery()
            .Produces<MapImportResultDto>()
            .Produces(400)
            .Produces(401);
    }
}

/// <summary>Result of a <c>POST /api/maps/import</c> call.</summary>
public sealed record MapImportResultDto(Guid MapId, bool IsNew);

/// <summary>Query, filter, sort and pagination parameters for the maps list.</summary>
public sealed record MapQueryParams(
    int Page = 1,
    int PageSize = 20,
    // Free-text search over song name, sub-name, author and mapper.
    string? Search = null,
    // Only maps having a difficulty with this characteristic (e.g. "Standard").
    string? Characteristic = null,
    BeatmapDifficultyRank? Difficulty = null,
    float? MinBpm = null,
    float? MaxBpm = null,
    int? MinDurationSeconds = null,
    int? MaxDurationSeconds = null,
    float? MinNps = null,
    float? MaxNps = null,
    // Computed difficulty rating in [0,1].
    double? MinDifficulty = null,
    double? MaxDifficulty = null,
    double? MinPp = null,
    double? MaxPp = null,
    // Ranked on ScoreSaber or BeatLeader.
    bool? Ranked = null,
    bool? Automapper = null,
    // A BeatSaver tag the map must carry (e.g. "nightcore").
    string? Tag = null,
    BeatSaverFetchStatus? FetchStatus = null,
    MapSortBy SortBy = MapSortBy.CreatedAt,
    SortDirection SortDir = SortDirection.Desc
);

public enum MapSortBy { CreatedAt, SongName, Bpm, Duration, Nps, Difficulty, Pp, Downloads, Upvotes, Score, Uploaded }

/// <summary>A map as it appears in the paginated list.</summary>
public sealed record MapListItemDto(
    Guid Id,
    string LevelId,
    string SongName,
    string? SongSubName,
    string SongAuthor,
    string Mapper,
    float Bpm,
    int DurationMs,
    string? CoverImageKey,
    string FetchStatus,
    DateTime CreatedAt,
    MapBeatSaverSummaryDto? BeatSaver,
    IList<MapListDifficultyDto> Difficulties
) {
    internal static MapListItemDto From(Beatmap b) => new(
        b.Id,
        b.LevelId,
        b.SongName,
        b.SongSubName,
        b.SongAuthor,
        b.Mapper,
        b.Bpm,
        b.DurationMs,
        b.CoverImageKey,
        b.FetchStatus.ToString(),
        b.CreatedAt,
        b.BeatSaverMap is null ? null : MapBeatSaverSummaryDto.From(b.BeatSaverMap),
        b.Difficulties
            .OrderBy(d => d.CharacteristicSerializedName)
            .ThenBy(d => d.DifficultyRank)
            .Select(MapListDifficultyDto.From)
            .ToList()
    );
}

/// <summary>Condensed BeatSaver info shown in a map list item.</summary>
public sealed record MapBeatSaverSummaryDto(
    bool Ranked,
    bool Automapper,
    int Downloads,
    int Upvotes,
    float Score,
    IList<string> Tags
) {
    internal static MapBeatSaverSummaryDto From(Shiron.BeatDash.DB.Schema.BeatSaver.BeatSaverMap m) => new(
        m.Ranked || m.BlRanked, m.Automapper, m.Stats.Downloads, m.Stats.Upvotes, m.Stats.Score, m.Tags);
}

/// <summary>A single difficulty summary within a map list item, with its computed metrics.</summary>
public sealed record MapListDifficultyDto(
    string DifficultyRank,
    string DifficultyName,
    string Characteristic,
    float NotesPerSecond,
    double? DifficultyRating,
    double? Pp
) {
    internal static MapListDifficultyDto From(BeatmapDifficulty d) => new(
        d.DifficultyRank.ToString(),
        d.DifficultyName,
        d.CharacteristicSerializedName,
        d.NotesPerSecond,
        d.Analysis?.DifficultyRating,
        d.Analysis?.Pp);
}

public sealed record MapDetailDto(
    Guid Id,
    string LevelId,
    string SongName,
    string? SongSubName,
    string SongAuthor,
    string Mapper,
    float Bpm,
    int DurationMs,
    string? CoverImageKey,
    string FetchStatus,
    DateTime? FetchLastAttemptedAt,
    string? FetchError,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IList<BeatmapDifficultyDto> Difficulties
) {
    internal static MapDetailDto From(Beatmap b) => new(
        b.Id,
        b.LevelId,
        b.SongName,
        b.SongSubName,
        b.SongAuthor,
        b.Mapper,
        b.Bpm,
        b.DurationMs,
        b.CoverImageKey,
        b.FetchStatus.ToString(),
        b.FetchLastAttemptedAt,
        b.FetchError,
        b.CreatedAt,
        b.UpdatedAt,
        b.Difficulties.Select(BeatmapDifficultyDto.From).ToList()
    );
}

public sealed record BeatmapDifficultyDto(
    Guid Id,
    string DifficultyRank,
    string DifficultyName,
    float NotesPerSecond,
    int CuttableObjectCount,
    int BombCount,
    int ObstacleCount,
    int LaneCount,
    float? NoteJumpSpeed,
    string CharacteristicSerializedName,
    int CharacteristicColorCount,
    bool CharacteristicRequires360Movement,
    bool CharacteristicContainsRotationEvents,
    DateTime CreatedAt
) {
    internal static BeatmapDifficultyDto From(BeatmapDifficulty d) => new(
        d.Id,
        d.DifficultyRank.ToString(),
        d.DifficultyName,
        d.NotesPerSecond,
        d.CuttableObjectCount,
        d.BombCount,
        d.ObstacleCount,
        d.LaneCount,
        d.NoteJumpSpeed,
        d.CharacteristicSerializedName,
        d.CharacteristicColorCount,
        d.CharacteristicRequires360Movement,
        d.CharacteristicContainsRotationEvents,
        d.CreatedAt
    );
}
