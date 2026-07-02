using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.API.Services;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.API.Endpoints;

public static class MapEndpoints {
    public static void MapMapEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup("/maps").WithTags("Maps");

        group.MapGet("/", async (
            BeatDashDbContext db,
            CancellationToken ct) => {
                var maps = await db.Beatmaps
                    .AsNoTracking()
                    .Include(b => b.Difficulties)
                    .OrderByDescending(b => b.CreatedAt)
                    .ToListAsync(ct);

                return Results.Ok(maps.Select(MapDetailDto.From).ToList());
            }).RequireAuthorization().Produces<IList<MapDetailDto>>();

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
    }
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
