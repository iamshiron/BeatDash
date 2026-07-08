using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shiron.BeatDash.API.Endpoints;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.API.Services;

/// <summary>
/// Computes a user's aggregate play statistics and skill profile. The aggregation is
/// keyed by user id so it can serve both the caller's own dashboard and public profiles.
/// </summary>
public interface IProfileStatsService {
    /// <summary>Aggregate play stats (tiles, rank distribution, activity, most played, recent/top).</summary>
    Task<UserStatsDto> GetStatsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Per-characteristic skill profile derived from played difficulties and accuracy.</summary>
    Task<SkillProfileDto> GetSkillAsync(Guid userId, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class ProfileStatsService(BeatDashDbContext db) : IProfileStatsService {
    /// <inheritdoc />
    public async Task<UserStatsDto> GetStatsAsync(Guid userId, CancellationToken ct = default) {
        // Completed, non-auto plays with final results — the basis for all aggregates.
        var baseQuery = db.PlaySessions
            .AsNoTracking()
            .Where(s =>
                s.UserId == userId &&
                !s.AutoMode &&
                s.EndReason == PlaySessionEndReason.Finished &&
                s.Results != null);

        var totalPlays = await baseQuery.CountAsync(ct);
        if (totalPlays == 0) {
            return new UserStatsDto(0, 0, 0, 0, 0, [], [], [], [], []);
        }

        var totalPlayTimeMs = await baseQuery.SumAsync(s => (long) s.Results!.EndSongTimeMs, ct);
        var averageAccuracy = await baseQuery.AverageAsync(s => s.Results!.Accuracy, ct);
        var fullCombos = await baseQuery.CountAsync(s => s.Results!.FullCombo, ct);
        var uniqueMaps = await baseQuery
            .Select(s => s.BeatmapDifficulty.BeatmapId)
            .Distinct()
            .CountAsync(ct);

        var rankDistribution = await baseQuery
            .GroupBy(s => s.Results!.Rank)
            .Select(g => new RankCountDto(g.Key, g.Count()))
            .ToListAsync(ct);

        // Daily play counts over the trailing ~26 weeks for the activity heatmap.
        var since = DateTime.UtcNow.Date.AddDays(-182);
        var activityRaw = await baseQuery
            .Where(s => s.StartedAt >= since)
            .GroupBy(s => s.StartedAt.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var activity = activityRaw
            .OrderBy(x => x.Day)
            .Select(x => new ActivityDayDto(DateOnly.FromDateTime(x.Day), x.Count))
            .ToList();

        var mostPlayedRaw = await baseQuery
            .GroupBy(s => s.BeatmapDifficulty.BeatmapId)
            .Select(g => new { BeatmapId = g.Key, PlayCount = g.Count() })
            .OrderByDescending(x => x.PlayCount)
            .Take(6)
            .ToListAsync(ct);
        var mostPlayedIds = mostPlayedRaw.Select(x => x.BeatmapId).ToList();
        var mostPlayedMaps = await db.Beatmaps
            .AsNoTracking()
            .Where(b => mostPlayedIds.Contains(b.Id))
            .Select(b => new { b.Id, b.SongName, b.SongAuthor, b.Mapper })
            .ToListAsync(ct);
        var mostPlayed = mostPlayedRaw
            .Select(x => {
                var m = mostPlayedMaps.First(mm => mm.Id == x.BeatmapId);
                return new MostPlayedMapDto(x.BeatmapId, m.SongName, m.SongAuthor, m.Mapper, x.PlayCount);
            })
            .ToList();

        var recentSessions = await baseQuery
            .OrderByDescending(s => s.StartedAt)
            .Include(s => s.BeatmapDifficulty.Beatmap)
            .Take(8)
            .ToListAsync(ct);

        var topScores = await baseQuery
            .OrderByDescending(s => s.Results!.Accuracy)
            .Include(s => s.BeatmapDifficulty.Beatmap)
            .Take(8)
            .ToListAsync(ct);

        return new UserStatsDto(
            totalPlays,
            totalPlayTimeMs,
            averageAccuracy,
            fullCombos,
            uniqueMaps,
            rankDistribution,
            activity,
            mostPlayed,
            recentSessions.Select(s => PlaySessionListItemDto.From(s)).ToList(),
            topScores.Select(s => PlaySessionListItemDto.From(s)).ToList());
    }

    /// <inheritdoc />
    public async Task<SkillProfileDto> GetSkillAsync(Guid userId, CancellationToken ct = default) {
        // Each completed non-auto play, with the difficulty it was played on.
        var plays = await db.PlaySessions
            .AsNoTracking()
            .Where(s =>
                s.UserId == userId &&
                !s.AutoMode &&
                s.EndReason == PlaySessionEndReason.Finished &&
                s.Results != null)
            .Select(s => new { s.BeatmapDifficultyId, s.Results!.Accuracy })
            .ToListAsync(ct);

        if (plays.Count == 0)
            return new SkillProfileDto([], 0);

        // Load characteristic vectors for the difficulties actually played.
        var difficultyIds = plays.Select(p => p.BeatmapDifficultyId).Distinct().ToList();
        var analyses = await db.BeatmapDifficultyAnalyses
            .AsNoTracking()
            .Where(a =>
                difficultyIds.Contains(a.BeatmapDifficultyId) &&
                a.MetricStatus == MetricStatus.Success &&
                a.Characteristics != null)
            .Select(a => new { a.BeatmapDifficultyId, a.Characteristics })
            .ToListAsync(ct);

        var charsByDifficulty = new Dictionary<Guid, Dictionary<string, double>>();
        foreach (var a in analyses) {
            var parsed = ParseCharacteristics(a.Characteristics);
            if (parsed != null) charsByDifficulty[a.BeatmapDifficultyId] = parsed;
        }

        // Accumulate per characteristic: exposure (map intensity) and
        // skill (intensity weighted by the accuracy you achieved).
        var exposure = new Dictionary<string, double>();
        var skill = new Dictionary<string, double>();
        var considered = 0;

        foreach (var play in plays) {
            if (!charsByDifficulty.TryGetValue(play.BeatmapDifficultyId, out var vector))
                continue;
            considered++;
            foreach (var (key, intensity) in vector) {
                exposure[key] = exposure.GetValueOrDefault(key) + intensity;
                skill[key] = skill.GetValueOrDefault(key) + intensity * play.Accuracy;
            }
        }

        if (considered == 0)
            return new SkillProfileDto([], 0);

        var characteristics = exposure.Keys
            .OrderBy(k => k)
            .Select(k => new SkillCharacteristicDto(
                k,
                skill[k] / considered,
                exposure[k] / considered))
            .ToList();

        return new SkillProfileDto(characteristics, considered);
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
