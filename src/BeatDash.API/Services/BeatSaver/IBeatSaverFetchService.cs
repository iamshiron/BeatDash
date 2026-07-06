using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;
using Shiron.BeatDash.DB.Schema.BeatSaver;

namespace Shiron.BeatDash.API.Services.BeatSaver;

/// <summary>
/// Fetches a beatmap's BeatSaver record, downloads its zip to object storage, and
/// persists the normalized data embedded on the <see cref="Beatmap"/>. Deduplicates
/// uploaders across maps and is safe to re-run (upsert semantics).
/// </summary>
public interface IBeatSaverFetchService {
    /// <summary>
    /// Fetches and persists a single beatmap. When <paramref name="force"/> is set,
    /// existing BeatSaver data is discarded and re-downloaded even if already present.
    /// </summary>
    /// <returns>The resulting fetch status.</returns>
    Task<BeatSaverFetchStatus> FetchAsync(Guid beatmapId, bool force, CancellationToken ct);

    /// <summary>
    /// Sweeps every beatmap that still needs a fetch (never fetched, or a retryable
    /// failure under the attempt cap) and processes each one through the rate limiter.
    /// </summary>
    /// <returns>The number of maps processed.</returns>
    Task<int> FetchPendingAsync(CancellationToken ct);
}

/// <summary>
/// <see cref="IDbContextFactory{TContext}"/>-backed implementation. Each map is
/// persisted with a short-lived context so no long-running scope is held during the
/// rate-limited network work.
/// </summary>
public sealed class BeatSaverFetchService(
    IDbContextFactory<BeatDashDbContext> dbFactory,
    IBeatSaverClient client,
    IStorageService storage,
    IOptions<StorageOptions> storageOptions,
    IOptions<BeatSaverOptions> beatSaverOptions,
    ILogger<BeatSaverFetchService> logger
) : IBeatSaverFetchService {

    private const string LevelIdPrefix = "custom_level_";
    private const string ZipKeyPrefix = "maps/";
    private const string ZipContentType = "application/zip";

    /// <inheritdoc/>
    public async Task<int> FetchPendingAsync(CancellationToken ct) {
        var opts = beatSaverOptions.Value;

        await using (var probe = await dbFactory.CreateDbContextAsync(ct)) {
            var query = probe.Beatmaps
                .AsNoTracking()
                .Where(b => b.FetchStatus == BeatSaverFetchStatus.Pending
                    || (b.FetchStatus == BeatSaverFetchStatus.Failed && b.FetchAttemptCount < opts.MaxFetchAttempts))
                .OrderBy(b => b.CreatedAt)
                .Select(b => b.Id);

            if (opts.MaxMapsPerRun > 0) query = query.Take(opts.MaxMapsPerRun);

            var ids = await query.ToListAsync(ct);
            if (ids.Count == 0) {
                logger.LogInformation("BeatSaver sweep: nothing to fetch");
                return 0;
            }

            logger.LogInformation("BeatSaver sweep: {Count} map(s) to fetch", ids.Count);

            var processed = 0;
            foreach (var id in ids) {
                ct.ThrowIfCancellationRequested();
                try {
                    await FetchAsync(id, force: false, ct);
                    processed++;
                } catch (Exception ex) when (ex is not OperationCanceledException) {
                    logger.LogError(ex, "BeatSaver sweep: unhandled error fetching map {MapId}", id);
                }
            }

            logger.LogInformation("BeatSaver sweep complete: processed {Count} map(s)", processed);
            return processed;
        }
    }

    /// <inheritdoc/>
    public async Task<BeatSaverFetchStatus> FetchAsync(Guid beatmapId, bool force, CancellationToken ct) {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var beatmap = await db.Beatmaps
            .Include(b => b.BeatSaverMap!)
            .ThenInclude(m => m.Versions)
            .FirstOrDefaultAsync(b => b.Id == beatmapId, ct);

        if (beatmap is null) {
            logger.LogWarning("BeatSaver fetch: beatmap {MapId} not found", beatmapId);
            return BeatSaverFetchStatus.NotFound;
        }

        if (!TryExtractHash(beatmap.LevelId, out var hash)) {
            logger.LogInformation(
                "BeatSaver fetch: level '{LevelId}' is not a custom map; skipping", beatmap.LevelId);
            beatmap.FetchStatus = BeatSaverFetchStatus.Skipped;
            beatmap.FetchLastAttemptedAt = DateTime.UtcNow;
            beatmap.FetchError = null;
            await db.SaveChangesAsync(ct);
            return BeatSaverFetchStatus.Skipped;
        }

        beatmap.FetchAttemptCount++;
        beatmap.FetchLastAttemptedAt = DateTime.UtcNow;

        try {
            var response = await client.GetMapByHashAsync(hash, ct);
            if (response is null) {
                beatmap.FetchStatus = BeatSaverFetchStatus.NotFound;
                beatmap.FetchError = null;
                await db.SaveChangesAsync(ct);
                return BeatSaverFetchStatus.NotFound;
            }

            await PersistAsync(db, beatmap, hash, response, ct);

            beatmap.FetchStatus = BeatSaverFetchStatus.Fetched;
            beatmap.FetchError = null;
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "BeatSaver fetch: persisted '{Name}' (key {Key}) for map {MapId}",
                response.Name, response.Id, beatmapId);
            return BeatSaverFetchStatus.Fetched;
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            logger.LogError(ex, "BeatSaver fetch failed for map {MapId} (hash {Hash})", beatmapId, hash);
            beatmap.FetchStatus = BeatSaverFetchStatus.Failed;
            beatmap.FetchError = Truncate(ex.Message, 1024);
            await db.SaveChangesAsync(ct);
            return BeatSaverFetchStatus.Failed;
        }
    }

    /// <summary>
    /// Upserts the uploader, downloads the zip, and replaces the beatmap's
    /// BeatSaver record and its versions/difficulties with fresh data.
    /// </summary>
    private async Task PersistAsync(
        BeatDashDbContext db, Beatmap beatmap, string hash, BeatSaverMapResponse response, CancellationToken ct) {

        // Drop any prior record; cascades remove old versions/difficulties.
        if (beatmap.BeatSaverMap is not null) {
            db.BeatSaverMaps.Remove(beatmap.BeatSaverMap);
            await db.SaveChangesAsync(ct);
            beatmap.BeatSaverMap = null;
        }

        var uploaderId = await UpsertUploaderAsync(db, response.Uploader, ct);

        // The version matching the queried hash carries the zip we want; fall back to the first.
        var version = response.Versions?.FirstOrDefault(v =>
                string.Equals(v.Hash, hash, StringComparison.OrdinalIgnoreCase))
            ?? response.Versions?.FirstOrDefault();

        string? zipKey = null;
        if (version?.DownloadUrl is { Length: > 0 } downloadUrl) {
            var zip = await client.DownloadAsync(downloadUrl, ct);
            if (zip is not null) {
                zipKey = $"{ZipKeyPrefix}{beatmap.Id}.zip";
                await storage.UploadAsync(storageOptions.Value.BucketAssets, zipKey, ZipContentType, zip, ct);
                logger.LogInformation(
                    "BeatSaver fetch: uploaded {Bytes}-byte zip to '{Key}'", zip.Length, zipKey);
            }
        } else {
            logger.LogWarning("BeatSaver fetch: no download URL for map {MapId}; zip not stored", beatmap.Id);
        }

        var meta = response.Metadata;
        var stats = response.Stats;

        var map = new BeatSaverMap {
            BeatmapId = beatmap.Id,
            BeatSaverId = response.Id ?? string.Empty,
            Name = TruncateRequired(response.Name ?? beatmap.SongName, 512),
            Description = response.Description,
            Automapper = response.Automapper,
            Ranked = response.Ranked,
            Qualified = response.Qualified,
            BlRanked = response.BlRanked,
            BlQualified = response.BlQualified,
            DeclaredAi = Truncate(response.DeclaredAi, 32),
            Uploaded = response.Uploaded?.UtcDateTime,
            BeatSaverCreatedAt = response.CreatedAt?.UtcDateTime,
            BeatSaverUpdatedAt = response.UpdatedAt?.UtcDateTime,
            LastPublishedAt = response.LastPublishedAt?.UtcDateTime,
            ZipObjectKey = zipKey,
            FetchedAt = DateTime.UtcNow,
            UploaderId = uploaderId,
            Tags = response.Tags?.Select(t => Truncate(t, 64)!).Where(t => t.Length > 0).ToList() ?? [],
            Metadata = new BeatSaverMetadata {
                Bpm = meta?.Bpm ?? beatmap.Bpm,
                Duration = meta?.Duration ?? 0,
                SongName = TruncateRequired(meta?.SongName ?? beatmap.SongName, 512),
                SongSubName = Truncate(meta?.SongSubName, 512),
                SongAuthorName = TruncateRequired(meta?.SongAuthorName ?? beatmap.SongAuthor, 512),
                LevelAuthorName = TruncateRequired(meta?.LevelAuthorName ?? beatmap.Mapper, 512),
            },
            Stats = new BeatSaverStats {
                Plays = stats?.Plays ?? 0,
                Downloads = stats?.Downloads ?? 0,
                Upvotes = stats?.Upvotes ?? 0,
                Downvotes = stats?.Downvotes ?? 0,
                Score = stats?.Score ?? 0,
            },
            Versions = MapVersions(response.Versions),
        };

        db.BeatSaverMaps.Add(map);
        beatmap.BeatSaverMap = map;
    }

    /// <summary>Finds or creates the shared uploader row, returning its id.</summary>
    private static async Task<Guid?> UpsertUploaderAsync(
        BeatDashDbContext db, BeatSaverUserResponse? uploader, CancellationToken ct) {
        if (uploader is null) return null;

        var existing = await db.BeatSaverUsers
            .FirstOrDefaultAsync(u => u.BeatSaverUserId == uploader.Id, ct);

        if (existing is null) {
            existing = new BeatSaverUser {
                BeatSaverUserId = uploader.Id,
                Name = TruncateRequired(uploader.Name ?? "Unknown", 256),
                Hash = Truncate(uploader.Hash, 128),
                Avatar = Truncate(uploader.Avatar, 1024),
                Type = Truncate(uploader.Type, 32),
                Admin = uploader.Admin,
                Curator = uploader.Curator,
                SeniorCurator = uploader.SeniorCurator,
                PlaylistUrl = Truncate(uploader.PlaylistUrl, 1024),
            };
            db.BeatSaverUsers.Add(existing);
        } else {
            existing.Name = TruncateRequired(uploader.Name ?? existing.Name, 256);
            existing.Hash = Truncate(uploader.Hash, 128);
            existing.Avatar = Truncate(uploader.Avatar, 1024);
            existing.Type = Truncate(uploader.Type, 32);
            existing.Admin = uploader.Admin;
            existing.Curator = uploader.Curator;
            existing.SeniorCurator = uploader.SeniorCurator;
            existing.PlaylistUrl = Truncate(uploader.PlaylistUrl, 1024);
            existing.UpdatedAt = DateTime.UtcNow;
        }

        return existing.Id;
    }

    private static List<BeatSaverVersion> MapVersions(IReadOnlyList<BeatSaverVersionResponse>? versions) {
        if (versions is null) return [];

        return versions.Select(v => new BeatSaverVersion {
            Hash = TruncateRequired(v.Hash ?? string.Empty, 128),
            Key = Truncate(v.Key, 32),
            State = Truncate(v.State, 32),
            CreatedAt = v.CreatedAt?.UtcDateTime,
            SageScore = v.SageScore,
            DownloadUrl = Truncate(v.DownloadUrl, 1024),
            CoverUrl = Truncate(v.CoverUrl, 1024),
            PreviewUrl = Truncate(v.PreviewUrl, 1024),
            Difficulties = v.Diffs?.Select(d => new BeatSaverVersionDifficulty {
                Njs = d.Njs,
                Offset = d.Offset,
                Notes = d.Notes,
                Bombs = d.Bombs,
                Obstacles = d.Obstacles,
                Nps = d.Nps,
                Length = d.Length,
                Characteristic = TruncateRequired(d.Characteristic ?? string.Empty, 64),
                Difficulty = TruncateRequired(d.Difficulty ?? string.Empty, 64),
                Events = d.Events,
                Chroma = d.Chroma,
                MappingExtensions = d.MappingExtensions,
                NoodleExtensions = d.NoodleExtensions,
                Cinema = d.Cinema,
                Seconds = d.Seconds,
                MaxScore = d.MaxScore,
                Environment = Truncate(d.Environment, 128),
                ParityErrors = d.ParitySummary?.Errors ?? 0,
                ParityWarns = d.ParitySummary?.Warns ?? 0,
                ParityResets = d.ParitySummary?.Resets ?? 0,
            }).ToList() ?? [],
        }).ToList();
    }

    /// <summary>
    /// Extracts the BeatSaver hash from a <c>custom_level_{hash}</c> level id.
    /// Returns <see langword="false"/> for non-custom or malformed ids.
    /// </summary>
    private static bool TryExtractHash(string levelId, out string hash) {
        hash = string.Empty;
        if (!levelId.StartsWith(LevelIdPrefix, StringComparison.OrdinalIgnoreCase)) return false;

        var remainder = levelId[LevelIdPrefix.Length..].Trim();
        if (remainder.Length == 0) return false;

        hash = remainder.ToLowerInvariant();
        return true;
    }

    private static string? Truncate(string? value, int maxLength) {
        if (value is null) return null;
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string TruncateRequired(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
