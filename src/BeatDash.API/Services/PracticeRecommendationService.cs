using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shiron.BeatDash.API.Endpoints;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.API.Services;

/// <summary>
/// Suggests maps to practice by matching the player's weak characteristics against
/// analyzed maps within an attainable difficulty band.
/// </summary>
public interface IPracticeRecommendationService {
    Task<IList<PracticeRecommendationDto>> GetRecommendationsAsync(
        Guid userId, int limit, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class PracticeRecommendationService(
    BeatDashDbContext db,
    IProfileStatsService profileStats
) : IPracticeRecommendationService {
    // Half-width of the difficulty band around the player's P75 rating.
    private const double BandHalfWidth = 0.1;
    // Maps played within this window are excluded to keep suggestions fresh.
    private static readonly TimeSpan RecentPlayWindow = TimeSpan.FromDays(14);
    // Candidate pool size scored in-process after the SQL-side filter.
    private const int CandidatePool = 400;

    /// <inheritdoc />
    public async Task<IList<PracticeRecommendationDto>> GetRecommendationsAsync(
        Guid userId, int limit, CancellationToken ct = default) {
        limit = Math.Clamp(limit, 1, 50);

        // Weakness vector: where the player is exposed but under-performs.
        var skill = await profileStats.GetSkillAsync(userId, ct);
        var weakness = skill.Characteristics
            .Select(c => (c.Key, Weight: Math.Max(0, c.Exposure - c.Skill)))
            .Where(x => x.Weight > 0)
            .ToDictionary(x => x.Key, x => x.Weight);

        var weightSum = weakness.Values.Sum();
        if (weightSum <= 0) return [];
        foreach (var key in weakness.Keys.ToList()) weakness[key] /= weightSum; // normalize

        // Attainable difficulty band = P75 of the player's completed difficulty ratings.
        var playedRatings = await db.PlaySessions
            .AsNoTracking()
            .Where(s =>
                s.UserId == userId &&
                !s.AutoMode &&
                s.EndReason == PlaySessionEndReason.Finished &&
                s.Results != null &&
                s.BeatmapDifficulty.Analysis != null &&
                s.BeatmapDifficulty.Analysis.DifficultyRating != null)
            .Select(s => s.BeatmapDifficulty.Analysis!.DifficultyRating!.Value)
            .ToListAsync(ct);

        double? ceiling = playedRatings.Count > 0 ? Percentile(playedRatings, 0.75) : null;
        var (bandMin, bandMax) = ceiling is double c
            ? (c - BandHalfWidth, c + BandHalfWidth)
            : (double.MinValue, double.MaxValue);

        // Difficulties the player already ran recently — excluded from suggestions.
        var since = DateTime.UtcNow - RecentPlayWindow;
        var recentlyPlayed = await db.PlaySessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.StartedAt >= since)
            .Select(s => s.BeatmapDifficultyId)
            .Distinct()
            .ToListAsync(ct);
        var recentSet = recentlyPlayed.ToHashSet();

        // Scored, analyzed candidates in the band; pool then ranked in-process.
        var candidates = await db.BeatmapDifficulties
            .AsNoTracking()
            .Where(d =>
                d.Analysis != null &&
                d.Analysis.MetricStatus == MetricStatus.Success &&
                d.Analysis.Characteristics != null &&
                d.Analysis.DifficultyRating != null &&
                d.Analysis.DifficultyRating >= bandMin &&
                d.Analysis.DifficultyRating <= bandMax)
            .Select(d => new {
                d.Id,
                d.BeatmapId,
                d.Beatmap.SongName,
                d.Beatmap.SongAuthor,
                d.Beatmap.Mapper,
                DifficultyRank = d.DifficultyRank.ToString(),
                d.DifficultyName,
                d.CharacteristicSerializedName,
                Rating = d.Analysis!.DifficultyRating!.Value,
                d.Analysis.Characteristics,
            })
            .Take(CandidatePool)
            .ToListAsync(ct);

        var scored = new List<(PracticeRecommendationDto Dto, double Score)>();
        foreach (var cand in candidates) {
            if (recentSet.Contains(cand.Id)) continue;
            var vector = ParseCharacteristics(cand.Characteristics);
            if (vector is null) continue;

            var match = vector.Sum(kv => weakness.GetValueOrDefault(kv.Key) * kv.Value);
            if (match <= 0) continue;

            var proximity = ceiling is double cc
                ? Math.Max(0, 1 - Math.Abs(cand.Rating - cc) / BandHalfWidth)
                : 1;
            var final = match * proximity;
            if (final <= 0) continue;

            // The 1–2 weak characteristics this map most addresses, for the UI reason.
            var targeted = vector
                .Where(kv => weakness.ContainsKey(kv.Key))
                .OrderByDescending(kv => weakness[kv.Key] * kv.Value)
                .Take(2)
                .Select(kv => kv.Key)
                .ToList();

            scored.Add((new PracticeRecommendationDto(
                cand.BeatmapId,
                cand.Id,
                cand.SongName,
                cand.SongAuthor,
                cand.Mapper,
                cand.DifficultyRank,
                cand.DifficultyName,
                cand.CharacteristicSerializedName,
                cand.Rating,
                final,
                targeted), final));
        }

        return scored
            .OrderByDescending(x => x.Score)
            .Take(limit)
            .Select(x => x.Dto)
            .ToList();
    }

    /// <summary>Linear-interpolated percentile of an unsorted sample.</summary>
    private static double Percentile(List<double> values, double p) {
        var sorted = values.OrderBy(v => v).ToList();
        if (sorted.Count == 1) return sorted[0];
        var rank = p * (sorted.Count - 1);
        var lo = (int) Math.Floor(rank);
        var hi = (int) Math.Ceiling(rank);
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (rank - lo);
    }

    private static Dictionary<string, double>? ParseCharacteristics(string? json) {
        if (string.IsNullOrEmpty(json)) return null;
        try {
            return JsonSerializer.Deserialize<Dictionary<string, double>>(json);
        } catch (JsonException) {
            return null;
        }
    }
}
