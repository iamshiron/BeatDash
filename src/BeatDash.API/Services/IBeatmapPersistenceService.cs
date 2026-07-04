using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.API.Services.Socket;
using Shiron.BeatDash.Data.Socket;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.API.Services;

/// <summary>
/// Persists a fully-joined map-start event (metadata + cover image) to the
/// database and object storage. Maps are deduplicated by <c>LevelId</c>;
/// difficulties by their characteristic/rank combination.
/// </summary>
public interface IBeatmapPersistenceService {
    /// <summary>
    /// Stores the beatmap (creating or updating it), uploads the cover image,
    /// and stores the difficulty variant. Safe to call for repeated plays of
    /// the same map.
    /// </summary>
    /// <returns>The database ID of the persisted beatmap.</returns>
    Task<Guid> PersistAsync(MapDataPair pair, CancellationToken ct);
}

/// <summary>
/// <see cref="IDbContextFactory{TContext}"/>-backed implementation. Each call
/// uses a short-lived context so the long-lived socket connection never holds
/// an open <see cref="BeatDashDbContext"/>.
/// </summary>
public sealed class BeatmapPersistenceService(
    IDbContextFactory<BeatDashDbContext> dbFactory,
    IStorageService storage,
    IOptions<StorageOptions> options,
    ILogger<BeatmapPersistenceService> logger
) : IBeatmapPersistenceService {

    private const string CoverContentType = "image/png";
    private const string CoverKeyPrefix = "covers/";

    /// <inheritdoc/>
    public async Task<Guid> PersistAsync(MapDataPair pair, CancellationToken ct) {
        var m = pair.Metadata;
        var bucket = options.Value.BucketAssets;

        logger.LogInformation(
            "PersistAsync START: song='{Song}', levelId='{LevelId}', userId={UserId}, imageBytes={Bytes}",
            m.SongName, m.LevelId, pair.UserId, pair.ImageBytes.Length);

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var beatmap = await db.Beatmaps.FirstOrDefaultAsync(b => b.LevelId == m.LevelId, ct);
        var isNew = beatmap is null;
        logger.LogInformation(
            "PersistAsync: beatmap lookup by LevelId='{LevelId}' → {Found} (isNew={IsNew})",
            m.LevelId, beatmap is not null ? $"found Id={beatmap.Id}" : "not found", isNew);

        if (isNew) {
            beatmap = new Beatmap {
                LevelId = m.LevelId,
                SongName = m.SongName,
                SongSubName = m.SongSubName,
                SongAuthor = m.SongAuthor,
                Mapper = m.Mapper,
                Bpm = m.Bpm,
                DurationMs = m.DurationMs,
                SubmittedByUserId = pair.UserId,
            };
            db.Beatmaps.Add(beatmap);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("PersistAsync: created beatmap Id={MapId}", beatmap.Id);
        } else {
            beatmap!.SongName = m.SongName;
            beatmap.SongSubName = m.SongSubName;
            beatmap.SongAuthor = m.SongAuthor;
            beatmap.Mapper = m.Mapper;
            beatmap.Bpm = m.Bpm;
            beatmap.DurationMs = m.DurationMs;
            beatmap.UpdatedAt = DateTime.UtcNow;
        }

        if (string.IsNullOrEmpty(beatmap.CoverImageKey)) {
            var coverKey = $"{CoverKeyPrefix}{beatmap.Id}.png";
            logger.LogInformation(
                "PersistAsync: uploading cover to MinIO bucket='{Bucket}', key='{Key}'",
                bucket, coverKey);
            await storage.UploadAsync(bucket, coverKey, CoverContentType, pair.ImageBytes, ct);
            beatmap.CoverImageKey = coverKey;
            logger.LogInformation("PersistAsync: cover uploaded, setting CoverImageKey");
        } else {
            logger.LogInformation("PersistAsync: cover already set ('{Key}'), skipping upload", beatmap.CoverImageKey);
        }

        if (Enum.TryParse(m.Difficulty, ignoreCase: true, out BeatmapDifficultyRank rank)) {
            await UpsertDifficultyAsync(db, beatmap.Id, pair.UserId, m, rank, ct);
            logger.LogInformation("PersistAsync: difficulty upserted (rank={Rank})", rank);
        } else {
            logger.LogWarning(
                "Unrecognized difficulty '{Difficulty}' for map '{Song}' ({LevelId}); difficulty not persisted",
                m.Difficulty, m.SongName, m.LevelId);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Persisted map '{Song}' ({MapId}){Action}",
            m.SongName, beatmap.Id, isNew ? " [new]" : "");

        return beatmap.Id;
    }

    /// <summary>
    /// Creates or updates the difficulty variant for the given beatmap.
    /// </summary>
    private static async Task UpsertDifficultyAsync(
        BeatDashDbContext db, Guid beatmapId, Guid userId, MapStartMessage m,
        BeatmapDifficultyRank rank, CancellationToken ct) {

        var diff = await db.BeatmapDifficulties.FirstOrDefaultAsync(
            d => d.BeatmapId == beatmapId
                && d.CharacteristicSerializedName == m.Characteristic.SerializedName
                && d.DifficultyRank == rank, ct);

        if (diff is null) {
            db.BeatmapDifficulties.Add(new BeatmapDifficulty {
                BeatmapId = beatmapId,
                SubmittedByUserId = userId,
                DifficultyRank = rank,
                DifficultyName = m.DifficultyName,
                NotesPerSecond = m.NotesPerSecond,
                CuttableObjectCount = m.CuttableObjectCount,
                BombCount = m.BombCount,
                ObstacleCount = m.ObstacleCount,
                LaneCount = m.LaneCount,
                NoteJumpSpeed = m.NoteJumpSpeed,
                CharacteristicSerializedName = m.Characteristic.SerializedName,
                CharacteristicColorCount = m.Characteristic.ColorCount,
                CharacteristicRequires360Movement = m.Characteristic.Requires360Movement,
                CharacteristicContainsRotationEvents = m.Characteristic.ContainsRotationEvents,
            });
            return;
        }

        diff.DifficultyName = m.DifficultyName;
        diff.NotesPerSecond = m.NotesPerSecond;
        diff.CuttableObjectCount = m.CuttableObjectCount;
        diff.BombCount = m.BombCount;
        diff.ObstacleCount = m.ObstacleCount;
        diff.LaneCount = m.LaneCount;
        diff.NoteJumpSpeed = m.NoteJumpSpeed;
        diff.CharacteristicColorCount = m.Characteristic.ColorCount;
        diff.CharacteristicRequires360Movement = m.Characteristic.Requires360Movement;
        diff.CharacteristicContainsRotationEvents = m.Characteristic.ContainsRotationEvents;
    }
}
