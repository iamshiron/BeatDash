using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shiron.BeatDash.Analysis;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.API.Endpoints;

/// <summary>
/// Admin-only sandbox for experimenting with the metric calibration
/// (<see cref="MetricConfig"/>). Exposes the running config for prefill and re-scores
/// selected maps against an arbitrary config supplied in the request — without ever
/// touching the singleton or persisting anything. Scoring reuses each difficulty's
/// already-extracted features, mirroring <c>BeatmapAnalysisService.RescoreStaleAsync</c>.
/// </summary>
public static class AdminMetricsEndpoints {
    /// <summary>Cap on how many maps a single score request may fan out over.</summary>
    private const int MaxMapsPerRequest = 25;

    public static void MapAdminMetricsEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup("/admin/metrics")
            .WithTags("Admin")
            .RequireAuthorization(p => p.RequireRole("Admin"));

        // Current running calibration + the feature catalogue, for prefilling the editor.
        group.MapGet("/config", (MetricConfig config, FeatureExtractor featureExtractor) => {
            var features = featureExtractor.Catalog
                .Select(f => new FeatureCatalogItemDto(f.Key, f.Description))
                .ToList();
            return Results.Ok(new MetricConfigResponse(config, features));
        }).Produces<MetricConfigResponse>();

        // Score selected maps against an arbitrary config. Read-only: builds a transient
        // scorer, reuses stored features, persists nothing.
        group.MapPost("/score", async (
            ScoreRequest request,
            BeatDashDbContext db,
            CancellationToken ct) => {
                if (request.Config is null) return Results.BadRequest("Missing config.");
                if (request.MapIds is null || request.MapIds.Length == 0)
                    return Results.BadRequest("Select at least one map.");

                var mapIds = request.MapIds.Distinct().Take(MaxMapsPerRequest).ToArray();

                var beatmaps = await db.Beatmaps
                    .AsNoTracking()
                    .Where(b => mapIds.Contains(b.Id))
                    .Include(b => b.Difficulties)
                    .ThenInclude(d => d.Analysis)
                    .ToListAsync(ct);

                var scorer = MetricScorer.CreateDefault(request.Config);

                // Preserve the caller's selection order.
                var byId = beatmaps.ToDictionary(b => b.Id);
                var maps = new List<ScoreMapDto>();
                foreach (var id in mapIds) {
                    if (!byId.TryGetValue(id, out var beatmap)) continue;

                    var difficulties = beatmap.Difficulties
                        .OrderBy(d => d.CharacteristicSerializedName)
                        .ThenBy(d => d.DifficultyRank)
                        .Select(d => ScoreDifficulty(d, scorer))
                        .ToArray();

                    maps.Add(new ScoreMapDto(beatmap.Id, beatmap.SongName, beatmap.SongAuthor, difficulties));
                }

                return Results.Ok(new ScoreResponse(maps.ToArray()));
            }).Produces<ScoreResponse>().Produces(400);
    }

    private static ScoreDifficultyDto ScoreDifficulty(BeatmapDifficulty difficulty, MetricScorer scorer) {
        var analysis = difficulty.Analysis;
        var features = DeserializeFeatures(analysis?.Features);

        if (features.Count == 0) {
            return new ScoreDifficultyDto(
                difficulty.Id, difficulty.CharacteristicSerializedName, difficulty.DifficultyRank,
                difficulty.DifficultyName, "FeaturesMissing",
                Difficulty: null, Pp: null,
                Characteristics: new Dictionary<string, double>(),
                Features: new Dictionary<string, double>(),
                StoredDifficulty: analysis?.DifficultyRating, StoredPp: analysis?.Pp);
        }

        var result = scorer.Score(features);
        var status = result.Outcome.ToString();
        double? diff = result.IsSuccess ? result.Metrics.GetValueOrDefault(MetricKeys.Difficulty) : null;
        double? pp = result.IsSuccess ? result.Metrics.GetValueOrDefault(MetricKeys.Pp) : null;
        var characteristics = result.IsSuccess
            ? new Dictionary<string, double>(result.Characteristics())
            : new Dictionary<string, double>();

        return new ScoreDifficultyDto(
            difficulty.Id, difficulty.CharacteristicSerializedName, difficulty.DifficultyRank,
            difficulty.DifficultyName, status,
            diff, pp, characteristics, features,
            analysis?.DifficultyRating, analysis?.Pp);
    }

    private static Dictionary<string, double> DeserializeFeatures(string? json) {
        if (string.IsNullOrEmpty(json)) return new Dictionary<string, double>();
        try {
            return JsonSerializer.Deserialize<Dictionary<string, double>>(json) ?? new Dictionary<string, double>();
        } catch (JsonException) {
            return new Dictionary<string, double>();
        }
    }
}

/// <summary>The running calibration plus the feature catalogue, for the editor UI.</summary>
public sealed record MetricConfigResponse(MetricConfig Config, IReadOnlyList<FeatureCatalogItemDto> Features);

/// <summary>One extractable feature the editor can weight against.</summary>
public sealed record FeatureCatalogItemDto(string Key, string Description);

/// <summary>A config to test and the maps to test it against.</summary>
public sealed record ScoreRequest(MetricConfig Config, Guid[] MapIds);

/// <summary>Scores for every requested map, in selection order.</summary>
public sealed record ScoreResponse(ScoreMapDto[] Maps);

/// <summary>Per-map results across all of its difficulties.</summary>
public sealed record ScoreMapDto(Guid MapId, string SongName, string SongAuthor, ScoreDifficultyDto[] Difficulties);

/// <summary>
/// One difficulty scored against the supplied config, with the raw features that drove
/// it and the stored DB baseline for delta display.
/// </summary>
public sealed record ScoreDifficultyDto(
    Guid DifficultyId,
    string Characteristic,
    BeatmapDifficultyRank Rank,
    string DifficultyName,
    // Success / Failed / NoFeatures / FeaturesMissing.
    string Status,
    double? Difficulty,
    double? Pp,
    Dictionary<string, double> Characteristics,
    Dictionary<string, double> Features,
    double? StoredDifficulty,
    double? StoredPp);
