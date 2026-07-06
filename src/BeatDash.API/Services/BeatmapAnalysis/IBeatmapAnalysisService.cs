using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shiron.BeatDash.Analysis;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.Beatmaps;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.API.Services.BeatmapAnalysis;

/// <summary>
/// Extracts a downloaded map zip, runs it through the beatmap parser, and stores
/// per-difficulty analysis linked to each <see cref="BeatmapDifficulty"/>. For now
/// it persists simple parsed map data (object counts + parse provenance); the same
/// entity will later carry the difficulty rating, PP and characteristic scores.
///
/// <para>Every attempted difficulty gets a row carrying a
/// <see cref="BeatmapAnalysisStatus"/>, so failures are recorded, not dropped.</para>
/// </summary>
public interface IBeatmapAnalysisService {
    /// <summary>
    /// Parses the map zip for the given beatmap and upserts a
    /// <see cref="BeatmapDifficultyAnalysis"/> for each of its difficulties.
    /// </summary>
    /// <returns>The number of difficulties analyzed successfully.</returns>
    Task<int> AnalyzeAsync(Guid beatmapId, CancellationToken ct);

    /// <summary>
    /// Re-scores every stored analysis whose metric calibration fingerprint no longer
    /// matches the running config, reusing the already-extracted features (no re-parse).
    /// A no-op when the calibration is unchanged.
    /// </summary>
    /// <returns>The number of difficulties re-scored.</returns>
    Task<int> RescoreStaleAsync(CancellationToken ct);
}

/// <summary>
/// <see cref="IDbContextFactory{TContext}"/>-backed implementation. Downloads the
/// zip from object storage using the beatmap's <c>BeatSaverMap.ZipObjectKey</c>.
/// </summary>
public sealed class BeatmapAnalysisService(
    IDbContextFactory<BeatDashDbContext> dbFactory,
    IStorageService storage,
    IOptions<StorageOptions> storageOptions,
    FeatureExtractor featureExtractor,
    MetricScorer metricScorer,
    MetricConfig metricConfig,
    ILogger<BeatmapAnalysisService> logger
) : IBeatmapAnalysisService {

    /// <summary>
    /// Bump when the parser or the persisted analysis fields change, so a re-run
    /// produces fresh rows.
    /// </summary>
    public const int AnalyzerVersion = 1;

    private readonly string _configHash = metricConfig.Fingerprint();

    /// <inheritdoc/>
    public async Task<int> AnalyzeAsync(Guid beatmapId, CancellationToken ct) {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var beatmap = await db.Beatmaps
            .Include(b => b.BeatSaverMap)
            .Include(b => b.Difficulties)
            .ThenInclude(d => d.Analysis)
            .FirstOrDefaultAsync(b => b.Id == beatmapId, ct);

        if (beatmap is null) {
            logger.LogWarning("Analysis: beatmap {MapId} not found", beatmapId);
            return 0;
        }

        if (beatmap.Difficulties.Count == 0) {
            logger.LogInformation("Analysis: beatmap {MapId} has no difficulties to analyze", beatmapId);
            return 0;
        }

        try {
            var zipKey = beatmap.BeatSaverMap?.ZipObjectKey;
            if (string.IsNullOrEmpty(zipKey)) {
                logger.LogInformation("Analysis: beatmap {MapId} has no downloaded zip", beatmapId);
                await RecordAllAsync(db, beatmap, BeatmapAnalysisStatus.ZipMissing, ct);
                return 0;
            }

            var zipBytes = await storage.DownloadAsync(storageOptions.Value.BucketAssets, zipKey, ct);
            if (zipBytes is null) {
                logger.LogWarning("Analysis: zip '{Key}' missing from storage for map {MapId}", zipKey, beatmapId);
                await RecordAllAsync(db, beatmap, BeatmapAnalysisStatus.ZipMissing, ct);
                return 0;
            }

            ParsedLevel level;
            try {
                using var stream = new MemoryStream(zipBytes, writable: false);
                using var source = new ZipBeatmapFileSource(stream, beatmap.LevelId);
                level = BeatmapParser.ParseLevel(source);
            } catch (Exception ex) when (ex is not OperationCanceledException) {
                logger.LogError(ex, "Analysis: failed to parse map zip for {MapId}", beatmapId);
                await RecordAllAsync(db, beatmap, BeatmapAnalysisStatus.ParseFailed, ct);
                return 0;
            }

            var analyzed = 0;
            foreach (var difficulty in beatmap.Difficulties) {
                var parsed = MatchDifficulty(level, difficulty);
                if (parsed is null) {
                    logger.LogWarning(
                        "Analysis: no parsed match for {Characteristic}/{Rank} in map {MapId}",
                        difficulty.CharacteristicSerializedName, difficulty.DifficultyRank, beatmapId);
                    Upsert(db, difficulty, BeatmapAnalysisStatus.DifficultyNotFound, level: null, parsed: null, features: null, metrics: null);
                    continue;
                }

                var features = featureExtractor.Extract(parsed, level.Bpm);
                var metrics = features.IsSuccess ? metricScorer.Score(features.Features) : null;
                Upsert(db, difficulty, BeatmapAnalysisStatus.Success, level, parsed, features, metrics);
                analyzed++;
            }

            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Analysis: {Success}/{Total} difficulties analyzed for map {MapId}",
                analyzed, beatmap.Difficulties.Count, beatmapId);
            return analyzed;
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            logger.LogError(ex, "Analysis: unexpected error for map {MapId}", beatmapId);
            await TryRecordFailedAsync(beatmapId, ct);
            return 0;
        }
    }

    /// <summary>Writes a single failure <paramref name="status"/> for every difficulty.</summary>
    private async Task RecordAllAsync(
        BeatDashDbContext db, Beatmap beatmap, BeatmapAnalysisStatus status, CancellationToken ct) {
        foreach (var difficulty in beatmap.Difficulties) {
            Upsert(db, difficulty, status, level: null, parsed: null, features: null, metrics: null);
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Best-effort recording of a general failure on a fresh context, used when an
    /// unexpected error may have left the primary context in an inconsistent state.
    /// </summary>
    private async Task TryRecordFailedAsync(Guid beatmapId, CancellationToken ct) {
        try {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var beatmap = await db.Beatmaps
                .Include(b => b.Difficulties)
                .ThenInclude(d => d.Analysis)
                .FirstOrDefaultAsync(b => b.Id == beatmapId, ct);
            if (beatmap is null) return;

            await RecordAllAsync(db, beatmap, BeatmapAnalysisStatus.Failed, ct);
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            logger.LogError(ex, "Analysis: failed to record failure status for map {MapId}", beatmapId);
        }
    }

    /// <summary>
    /// Finds the parsed difficulty matching a persisted one by characteristic and
    /// difficulty rank (the parsed difficulty name maps directly onto the rank enum).
    /// </summary>
    private static ParsedBeatmap? MatchDifficulty(ParsedLevel level, BeatmapDifficulty difficulty) {
        foreach (var pb in level.Beatmaps) {
            if (!string.Equals(pb.Characteristic, difficulty.CharacteristicSerializedName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (Enum.TryParse<BeatmapDifficultyRank>(pb.Difficulty, ignoreCase: true, out var rank)
                && rank == difficulty.DifficultyRank) {
                return pb;
            }
        }
        return null;
    }

    /// <summary>
    /// Creates or updates the analysis row for a difficulty. Parsed data is written
    /// only on success; failure rows clear it so stale values never linger.
    /// </summary>
    private void Upsert(
        BeatDashDbContext db, BeatmapDifficulty difficulty, BeatmapAnalysisStatus status,
        ParsedLevel? level, ParsedBeatmap? parsed, FeatureExtractionResult? features, MetricResult? metrics) {
        var analysis = difficulty.Analysis;
        var isNew = analysis is null;
        analysis ??= new BeatmapDifficultyAnalysis {
            BeatmapDifficultyId = difficulty.Id, Status = status, AnalyzerVersion = AnalyzerVersion,
        };

        analysis.Status = status;

        if (level is not null && parsed is not null) {
            var counts = parsed.Counts;
            analysis.NoteCount = counts["notes"];
            analysis.BombCount = counts["bombs"];
            analysis.ObstacleCount = counts["obstacles"];
            analysis.ChainCount = counts["chains"];
            analysis.ArcCount = counts["arcs"];
            analysis.BpmChangeCount = counts["bpm_changes"];
            analysis.Bpm = level.Bpm;
            analysis.Njs = parsed.Njs;
            analysis.NjsOffset = parsed.NjsOffset;
            analysis.FormatVersion = parsed.FormatVersion;
        } else {
            analysis.NoteCount = null;
            analysis.BombCount = null;
            analysis.ObstacleCount = null;
            analysis.ChainCount = null;
            analysis.ArcCount = null;
            analysis.BpmChangeCount = null;
            analysis.Bpm = null;
            analysis.Njs = null;
            analysis.NjsOffset = null;
            analysis.FormatVersion = null;
        }

        if (features is null) {
            analysis.FeatureStatus = FeatureExtractionStatus.NotAttempted;
            analysis.Features = null;
        } else {
            analysis.FeatureStatus = MapFeatureStatus(features.Outcome);
            analysis.Features = features.IsSuccess ? FeatureJson.Serialize(features.Features) : null;
        }

        WriteMetrics(analysis, metrics);

        analysis.AnalyzerVersion = AnalyzerVersion;
        analysis.AnalyzedAt = DateTime.UtcNow;

        if (isNew) {
            db.BeatmapDifficultyAnalyses.Add(analysis);
            difficulty.Analysis = analysis;
        }
    }

    /// <summary>
    /// Writes the metric fields and stamps the calibration fingerprint. Stale-metric
    /// detection keys off <see cref="BeatmapDifficultyAnalysis.MetricConfigHash"/>, so
    /// every attempt (success or failure) records the config it ran under.
    /// </summary>
    private void WriteMetrics(BeatmapDifficultyAnalysis analysis, MetricResult? metrics) {
        if (metrics is null) {
            analysis.MetricStatus = MetricStatus.NotAttempted;
            analysis.DifficultyRating = null;
            analysis.Pp = null;
            analysis.Characteristics = null;
            analysis.MetricConfigHash = null;
            return;
        }

        analysis.MetricConfigHash = _configHash;

        if (metrics.IsSuccess) {
            analysis.MetricStatus = MetricStatus.Success;
            analysis.DifficultyRating = metrics.Metrics.GetValueOrDefault(MetricKeys.Difficulty);
            analysis.Pp = metrics.Metrics.GetValueOrDefault(MetricKeys.Pp);
            analysis.Characteristics = FeatureJson.Serialize(metrics.Characteristics());
        } else {
            analysis.MetricStatus = MapMetricStatus(metrics.Outcome);
            analysis.DifficultyRating = null;
            analysis.Pp = null;
            analysis.Characteristics = null;
        }
    }

    /// <inheritdoc/>
    public async Task<int> RescoreStaleAsync(CancellationToken ct) {
        const int batchSize = 500;
        var total = 0;

        while (true) {
            ct.ThrowIfCancellationRequested();
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            // Rows that have features but were scored under a different calibration.
            var batch = await db.BeatmapDifficultyAnalyses
                .Where(a => a.Features != null && a.MetricConfigHash != _configHash)
                .Take(batchSize)
                .ToListAsync(ct);
            if (batch.Count == 0) break;

            foreach (var analysis in batch) {
                var features = DeserializeFeatures(analysis.Features);
                var metrics = metricScorer.Score(features);
                WriteMetrics(analysis, metrics);
                analysis.AnalyzedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync(ct);
            total += batch.Count;
        }

        if (total > 0) {
            logger.LogInformation("Rescore: recomputed metrics for {Count} difficulties at calibration {Hash}", total, _configHash);
        }
        return total;
    }

    private static IReadOnlyDictionary<string, double> DeserializeFeatures(string? json) {
        if (string.IsNullOrEmpty(json)) return new Dictionary<string, double>();
        try {
            return JsonSerializer.Deserialize<Dictionary<string, double>>(json) ?? new Dictionary<string, double>();
        } catch (JsonException) {
            return new Dictionary<string, double>();
        }
    }

    private static FeatureExtractionStatus MapFeatureStatus(FeatureExtractionOutcome outcome) => outcome switch {
        FeatureExtractionOutcome.Success => FeatureExtractionStatus.Success,
        FeatureExtractionOutcome.NoNotes => FeatureExtractionStatus.NoNotes,
        FeatureExtractionOutcome.InvalidTiming => FeatureExtractionStatus.InvalidTiming,
        _ => FeatureExtractionStatus.Failed,
    };

    private static MetricStatus MapMetricStatus(MetricOutcome outcome) => outcome switch {
        MetricOutcome.Success => MetricStatus.Success,
        MetricOutcome.NoFeatures => MetricStatus.NoFeatures,
        _ => MetricStatus.Failed,
    };
}
