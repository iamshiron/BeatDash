using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shiron.BeatDash.API;
using Shiron.BeatDash.API.Endpoints;
using Shiron.BeatDash.API.Services.Health;
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

    /// <summary>Weekly skill-profile time series over the trailing <paramref name="weeks"/> weeks.</summary>
    Task<SkillProgressionDto> GetSkillProgressionAsync(Guid userId, int weeks, CancellationToken ct = default);

    /// <summary>
    /// Career-wide weakness profile built from the pre-materialized note aggregate.
    /// When <paramref name="characteristic"/> is set, restricts to that game mode.
    /// </summary>
    Task<WeaknessProfileDto> GetWeaknessAsync(Guid userId, string? characteristic, CancellationToken ct = default);

    /// <summary>
    /// Post-session recap for a finished session: deltas vs the previous attempt and
    /// the player's average on the same difficulty. Null if not found or not owned.
    /// </summary>
    Task<SessionRecapDto?> GetRecapAsync(Guid userId, Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// Summary of the player's most recent "sitting" — the latest contiguous cluster
    /// of plays with no long idle gap between them. Null if they've never played.
    /// </summary>
    Task<SessionSummaryDto?> GetLatestSessionSummaryAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// All of the player's sittings (clusters of plays with no long idle gap), newest first
    /// and paginated. Each carries its plays plus aggregate stats, including calories when
    /// health tracking is on.
    /// </summary>
    Task<PagedResult<SessionSummaryDto>> GetSittingsAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);
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

    /// <inheritdoc />
    public async Task<SkillProgressionDto> GetSkillProgressionAsync(Guid userId, int weeks, CancellationToken ct = default) {
        weeks = Math.Clamp(weeks, 1, 52);
        var today = DateTime.UtcNow.Date;
        // Monday of the current week, then back (weeks - 1) weeks for the window start.
        var weekStart = today.AddDays(-((int) today.DayOfWeek + 6) % 7);
        var windowStart = weekStart.AddDays(-(weeks - 1) * 7);

        var plays = await db.PlaySessions
            .AsNoTracking()
            .Where(s =>
                s.UserId == userId &&
                !s.AutoMode &&
                s.EndReason == PlaySessionEndReason.Finished &&
                s.Results != null &&
                s.StartedAt >= windowStart)
            .Select(s => new { s.BeatmapDifficultyId, s.Results!.Accuracy, s.StartedAt })
            .ToListAsync(ct);

        // Empty-but-shaped result so the client can still render an axis for every week.
        var emptyWeeks = Enumerable.Range(0, weeks)
            .Select(i => new SkillProgressionWeekDto(
                DateOnly.FromDateTime(windowStart.AddDays(i * 7)), [], 0))
            .ToList();

        if (plays.Count == 0)
            return new SkillProgressionDto(emptyWeeks, []);

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

        var allKeys = new SortedSet<string>();
        var weekBuckets = new List<SkillProgressionWeekDto>(weeks);
        for (var i = 0; i < weeks; i++) {
            var bucketStart = windowStart.AddDays(i * 7);
            var bucketEnd = bucketStart.AddDays(7);

            var exposure = new Dictionary<string, double>();
            var skill = new Dictionary<string, double>();
            var considered = 0;

            foreach (var play in plays) {
                if (play.StartedAt < bucketStart || play.StartedAt >= bucketEnd) continue;
                if (!charsByDifficulty.TryGetValue(play.BeatmapDifficultyId, out var vector)) continue;
                considered++;
                foreach (var (key, intensity) in vector) {
                    exposure[key] = exposure.GetValueOrDefault(key) + intensity;
                    skill[key] = skill.GetValueOrDefault(key) + intensity * play.Accuracy;
                    allKeys.Add(key);
                }
            }

            var characteristics = considered == 0
                ? new List<SkillCharacteristicDto>()
                : exposure.Keys
                    .OrderBy(k => k)
                    .Select(k => new SkillCharacteristicDto(k, skill[k] / considered, exposure[k] / considered))
                    .ToList();

            weekBuckets.Add(new SkillProgressionWeekDto(
                DateOnly.FromDateTime(bucketStart), characteristics, considered));
        }

        return new SkillProgressionDto(weekBuckets, allKeys.ToList());
    }

    /// <inheritdoc />
    public async Task<WeaknessProfileDto> GetWeaknessAsync(Guid userId, string? characteristic, CancellationToken ct = default) {
        // The full per-user aggregate is small (≤ ~1k rows); pull it once and build
        // both marginals + weak spots in-process.
        var rows = await db.PlayNoteAggregates
            .AsNoTracking()
            .Where(a => a.UserId == userId &&
                (characteristic == null || a.CharacteristicSerializedName == characteristic))
            .Select(a => new AggregateRow(
                a.CharacteristicSerializedName,
                (int) a.ColorType,
                (int) a.CutDirection,
                a.LineIndex,
                a.NoteLineLayer,
                a.NoteCount,
                a.MissCount,
                a.SumEarnedScore,
                a.SumMaxScore))
            .ToListAsync(ct);

        if (rows.Count == 0)
            return new WeaknessProfileDto([], [], [], 0);

        var cutDirectionMatrix = rows
            .GroupBy(r => new { r.Hand, r.CutDirection })
            .Select(g => new CutDirectionCellDto(
                g.Key.Hand,
                g.Key.CutDirection,
                Accuracy(g.Sum(x => x.SumEarnedScore), g.Sum(x => x.SumMaxScore)),
                MissRate(g.Sum(x => x.MissCount), g.Sum(x => x.NoteCount)),
                g.Sum(x => x.NoteCount)))
            .OrderBy(c => c.Hand).ThenBy(c => c.CutDirection)
            .ToList();

        var gridHeatmap = rows
            .GroupBy(r => new { r.Hand, r.LineIndex, r.NoteLineLayer })
            .Select(g => new GridCellDto(
                g.Key.Hand,
                g.Key.LineIndex,
                g.Key.NoteLineLayer,
                Accuracy(g.Sum(x => x.SumEarnedScore), g.Sum(x => x.SumMaxScore)),
                MissRate(g.Sum(x => x.MissCount), g.Sum(x => x.NoteCount)),
                g.Sum(x => x.NoteCount)))
            .OrderBy(c => c.Hand).ThenBy(c => c.NoteLineLayer).ThenBy(c => c.LineIndex)
            .ToList();

        const long minKeySamples = 10; // ignore thin cells when picking the weakest spot
        var weakSpots = rows
            .GroupBy(r => r.Characteristic)
            .Select(g => {
                var weakest = g
                    .Where(r => r.NoteCount >= minKeySamples && r.SumMaxScore > 0)
                    .OrderBy(r => Accuracy(r.SumEarnedScore, r.SumMaxScore))
                    .Select(r => (AggregateRow?) r)
                    .FirstOrDefault();
                return new CharacteristicWeakSpotDto(
                    g.Key,
                    Accuracy(g.Sum(x => x.SumEarnedScore), g.Sum(x => x.SumMaxScore)),
                    MissRate(g.Sum(x => x.MissCount), g.Sum(x => x.NoteCount)),
                    weakest?.CutDirection ?? -1,
                    weakest?.LineIndex ?? -1,
                    weakest?.NoteLineLayer ?? -1);
            })
            .OrderBy(w => w.Characteristic)
            .ToList();

        return new WeaknessProfileDto(cutDirectionMatrix, gridHeatmap, weakSpots, rows.Sum(r => r.NoteCount));
    }

    /// <inheritdoc />
    public async Task<SessionRecapDto?> GetRecapAsync(Guid userId, Guid sessionId, CancellationToken ct = default) {
        var session = await db.PlaySessions
            .AsNoTracking()
            .Include(s => s.BeatmapDifficulty.Beatmap)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, ct);

        if (session?.Results is null) return null;

        var noteCount = await db.PlaySessionNoteItems.CountAsync(i => i.PlaySessionId == sessionId, ct);
        var comboBreakCount = await db.PlaySessionComboBreakItems.CountAsync(i => i.PlaySessionId == sessionId, ct);
        var hasMotionData = await db.PlaySessionItemMotionFrames.AnyAsync(i => i.PlaySessionId == sessionId, ct);
        var hasMotionSummary = await db.PlaySessionMotionSummaries.AnyAsync(i => i.PlaySessionId == sessionId, ct);
        var detail = PlaySessionDetailDto.From(session, noteCount, comboBreakCount, hasMotionData, hasMotionSummary);

        // All the player's completed non-auto attempts on this exact difficulty.
        var attempts = await db.PlaySessions
            .AsNoTracking()
            .Where(s =>
                s.UserId == userId &&
                s.BeatmapDifficultyId == session.BeatmapDifficultyId &&
                !s.AutoMode &&
                s.EndReason == PlaySessionEndReason.Finished &&
                s.Results != null)
            .Select(s => new AttemptResult(
                s.StartedAt,
                s.Results!.Score,
                s.Results.Accuracy,
                s.Results.Rank,
                s.Results.MaxCombo,
                s.Results.Misses,
                s.Results.MultipliedScore,
                s.Results.MaxPossibleScore,
                s.Results.FullCombo,
                s.Results.BadCuts,
                s.Results.GoodCuts,
                s.Results.FinalEnergy,
                s.Results.EndSongTimeMs))
            .ToListAsync(ct);

        var target = session.Results;

        // Immediately preceding attempt (by start time) before this session.
        var previous = attempts
            .Where(a => a.StartedAt < session.StartedAt)
            .OrderByDescending(a => a.StartedAt)
            .Select(a => (AttemptResult?) a)
            .FirstOrDefault();

        // Best score among prior attempts — used for the PB comparison + badge.
        var priorBest = attempts
            .Where(a => a.StartedAt < session.StartedAt)
            .OrderByDescending(a => a.Score)
            .Select(a => (AttemptResult?) a)
            .FirstOrDefault();

        var isNewPersonalBest = target.Score > (priorBest?.Score ?? int.MinValue);

        var vsPrevious = previous is { } p
            ? new RecapDeltaDto(
                target.Score - p.Score,
                target.Accuracy - p.Accuracy,
                target.MaxCombo - p.MaxCombo,
                target.Misses - p.Misses)
            : new RecapDeltaDto(0, 0, 0, 0);

        // Average across all attempts (including this one) on the difficulty.
        var vsAverage = new RecapDeltaDto(
            target.Score - (int) Math.Round(attempts.Average(a => (double) a.Score)),
            target.Accuracy - (float) attempts.Average(a => a.Accuracy),
            target.MaxCombo - (int) Math.Round(attempts.Average(a => (double) a.MaxCombo)),
            target.Misses - (int) Math.Round(attempts.Average(a => (double) a.Misses)));

        return new SessionRecapDto(
            detail,
            ToResultsDto(previous),
            ToResultsDto(priorBest),
            vsPrevious,
            vsAverage,
            isNewPersonalBest);
    }

    // Plays farther apart than this start a new sitting.
    private static readonly TimeSpan SittingGap = TimeSpan.FromMinutes(45);

    /// <inheritdoc />
    public async Task<SessionSummaryDto?> GetLatestSessionSummaryAsync(Guid userId, CancellationToken ct = default) {
        // Newest completed non-auto plays; capped since a sitting is a short recent window.
        var recent = await db.PlaySessions
            .AsNoTracking()
            .Where(s =>
                s.UserId == userId &&
                !s.AutoMode &&
                s.EndReason == PlaySessionEndReason.Finished &&
                s.Results != null)
            .OrderByDescending(s => s.StartedAt)
            .Include(s => s.BeatmapDifficulty.Beatmap)
            .Take(200)
            .ToListAsync(ct);

        if (recent.Count == 0) return null;

        // Walk back from the most recent play while the idle gap stays small.
        var sitting = new List<PlaySession> { recent[0] };
        var earliestStart = recent[0].StartedAt;
        for (var i = 1; i < recent.Count; i++) {
            var play = recent[i];
            var playEnd = play.EndedAt ?? play.StartedAt;
            if (earliestStart - playEnd > SittingGap) break;
            sitting.Add(play);
            if (play.StartedAt < earliestStart) earliestStart = play.StartedAt;
        }
        sitting.Reverse(); // chronological

        // Best score per difficulty across all plays, to flag PBs achieved this sitting.
        var difficultyIds = sitting.Select(s => s.BeatmapDifficultyId).Distinct().ToList();
        var bestScores = await BestScoresByDifficultyAsync(userId, difficultyIds, ct);
        var (motion, cal) = await LoadSittingContextAsync(userId, sitting.Select(s => s.Id).ToList(), ct);

        bool IsPb(PlaySession s) =>
            bestScores.TryGetValue(s.BeatmapDifficultyId, out var max) && s.Results!.Score == max;

        return Summarize(sitting, motion, cal, IsPb);
    }

    /// <inheritdoc />
    public async Task<PagedResult<SessionSummaryDto>> GetSittingsAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default) {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        // Lightweight timeline of every completed play, grouped into sittings by idle gap.
        var timeline = await db.PlaySessions
            .AsNoTracking()
            .Where(s =>
                s.UserId == userId &&
                !s.AutoMode &&
                s.EndReason == PlaySessionEndReason.Finished &&
                s.Results != null)
            .OrderBy(s => s.StartedAt)
            .Select(s => new { s.Id, s.StartedAt, s.EndedAt })
            .ToListAsync(ct);
        if (timeline.Count == 0) return PagedResult<SessionSummaryDto>.Empty(page, pageSize);

        var sittings = new List<List<Guid>>();
        var current = new List<Guid> { timeline[0].Id };
        var prevEnd = timeline[0].EndedAt ?? timeline[0].StartedAt;
        for (var i = 1; i < timeline.Count; i++) {
            var t = timeline[i];
            if (t.StartedAt - prevEnd > SittingGap) {
                sittings.Add(current);
                current = [];
            }
            current.Add(t.Id);
            prevEnd = t.EndedAt ?? t.StartedAt;
        }
        sittings.Add(current);
        sittings.Reverse(); // newest first

        var totalCount = sittings.Count;
        var totalPages = (int) Math.Ceiling(totalCount / (double) pageSize);
        var pageSittings = sittings.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        if (pageSittings.Count == 0)
            return new PagedResult<SessionSummaryDto>([], totalCount, page, pageSize, totalPages);

        // Hydrate only the plays on this page.
        var pagePlayIds = pageSittings.SelectMany(x => x).ToList();
        var plays = await db.PlaySessions
            .AsNoTracking()
            .Where(s => pagePlayIds.Contains(s.Id))
            .Include(s => s.BeatmapDifficulty.Beatmap)
            .ToListAsync(ct);
        var playById = plays.ToDictionary(p => p.Id);

        var difficultyIds = plays.Select(p => p.BeatmapDifficultyId).Distinct().ToList();
        var bestScores = await BestScoresByDifficultyAsync(userId, difficultyIds, ct);
        var (motion, cal) = await LoadSittingContextAsync(userId, pagePlayIds, ct);

        bool IsPb(PlaySession s) =>
            bestScores.TryGetValue(s.BeatmapDifficultyId, out var max) && s.Results!.Score == max;

        var items = pageSittings
            .Select(ids => Summarize(
                ids.Select(id => playById[id]).OrderBy(p => p.StartedAt).ToList(),
                motion, cal, IsPb))
            .ToList();

        return new PagedResult<SessionSummaryDto>(items, totalCount, page, pageSize, totalPages);
    }

    /// <summary>Highest completed score per difficulty for the user, for PB flags.</summary>
    private Task<Dictionary<Guid, int>> BestScoresByDifficultyAsync(
        Guid userId, List<Guid> difficultyIds, CancellationToken ct) =>
        db.PlaySessions
            .AsNoTracking()
            .Where(s =>
                s.UserId == userId &&
                !s.AutoMode &&
                s.EndReason == PlaySessionEndReason.Finished &&
                s.Results != null &&
                difficultyIds.Contains(s.BeatmapDifficultyId))
            .GroupBy(s => s.BeatmapDifficultyId)
            .Select(g => new { DifficultyId = g.Key, MaxScore = g.Max(x => x.Results!.Score) })
            .ToDictionaryAsync(x => x.DifficultyId, x => x.MaxScore, ct);

    /// <summary>Loads per-play motion + the user's calorie inputs for a set of plays.</summary>
    private async Task<(Dictionary<Guid, SittingMotion> Motion, CalorieContext? Cal)>
        LoadSittingContextAsync(Guid userId, List<Guid> playIds, CancellationToken ct) {
        var motion = await db.PlaySessionMotionSummaries
            .AsNoTracking()
            .Where(m => playIds.Contains(m.PlaySessionId))
            .Select(m => new {
                m.PlaySessionId,
                m.LeftSaberTravel,
                m.RightSaberTravel,
                m.AvgLeftSaberSpeed,
                m.AvgRightSaberSpeed,
                m.HeadTravel
            })
            .ToDictionaryAsync(
                m => m.PlaySessionId,
                m => new SittingMotion(
                    m.LeftSaberTravel,
                    m.RightSaberTravel,
                    (m.AvgLeftSaberSpeed + m.AvgRightSaberSpeed) / 2,
                    m.HeadTravel),
                ct);

        var user = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.HealthTrackingEnabled, u.WeightKg, u.BirthYear, u.Sex })
            .FirstOrDefaultAsync(ct);

        CalorieContext? cal = user is { HealthTrackingEnabled: true, WeightKg: { } w }
            ? new CalorieContext(w, user.BirthYear is { } by ? DateTime.UtcNow.Year - by : null, user.Sex)
            : null;

        return (motion, cal);
    }

    /// <summary>Builds a sitting summary from its (chronological) plays and the loaded context.</summary>
    private static SessionSummaryDto Summarize(
        List<PlaySession> sitting,
        Dictionary<Guid, SittingMotion> motion,
        CalorieContext? cal,
        Func<PlaySession, bool> isPb) {
        var totalSaberTravel = sitting.Sum(s =>
            motion.TryGetValue(s.Id, out var m) ? m.LeftTravel + m.RightTravel : 0);

        double? kcal = null;
        double? minutes = null;
        if (cal is { } c) {
            double k = 0, mins = 0;
            foreach (var s in sitting) {
                var hasMotion = motion.TryGetValue(s.Id, out var m);
                var est = CalorieEstimator.Estimate(
                    c.WeightKg,
                    s.Results!.EndSongTimeMs,
                    hasMotion ? m.AvgSpeed : null,
                    hasMotion ? m.HeadTravel : null,
                    s.BeatmapDifficulty.NotesPerSecond,
                    avgHr: null,
                    c.Age,
                    c.Sex);
                k += est.Kcal;
                mins += est.ActiveMinutes;
            }
            kcal = k;
            minutes = mins;
        }

        var best = sitting.OrderByDescending(s => s.Results!.Accuracy).First();

        return new SessionSummaryDto(
            sitting.First().StartedAt,
            sitting.Max(s => s.EndedAt ?? s.StartedAt),
            sitting.Count,
            sitting.Sum(s => (long) s.Results!.EndSongTimeMs),
            sitting.Average(s => s.Results!.Accuracy),
            sitting.Count(s => s.Results!.FullCombo),
            sitting.Select(s => s.BeatmapDifficulty.BeatmapId).Distinct().Count(),
            sitting.Count(isPb),
            totalSaberTravel,
            sitting
                .GroupBy(s => s.Results!.Rank)
                .Select(g => new RankCountDto(g.Key, g.Count()))
                .ToList(),
            sitting.Select(s => PlaySessionListItemDto.From(s, isPb(s))).ToList(),
            PlaySessionListItemDto.From(best, isPb(best)),
            kcal,
            minutes);
    }

    private readonly record struct SittingMotion(
        double LeftTravel, double RightTravel, double AvgSpeed, double HeadTravel);

    private readonly record struct CalorieContext(double WeightKg, int? Age, string? Sex);

    private static PlaySessionResultsDto? ToResultsDto(AttemptResult? a) => a is { } r
        ? new PlaySessionResultsDto(
            r.Score, r.MultipliedScore, r.MaxPossibleScore, r.Accuracy, r.Rank,
            r.FullCombo, r.MaxCombo, r.GoodCuts, r.BadCuts, r.Misses, r.FinalEnergy, r.EndSongTimeMs)
        : null;

    private readonly record struct AttemptResult(
        DateTime StartedAt,
        int Score,
        float Accuracy,
        string Rank,
        int MaxCombo,
        int Misses,
        int MultipliedScore,
        int MaxPossibleScore,
        bool FullCombo,
        int BadCuts,
        int GoodCuts,
        float FinalEnergy,
        int EndSongTimeMs);

    private static double Accuracy(long earned, long max) => max > 0 ? (double) earned / max : 0;
    private static double MissRate(long misses, long notes) => notes > 0 ? (double) misses / notes : 0;

    private readonly record struct AggregateRow(
        string Characteristic,
        int Hand,
        int CutDirection,
        int LineIndex,
        int NoteLineLayer,
        long NoteCount,
        long MissCount,
        long SumEarnedScore,
        long SumMaxScore);

    private static Dictionary<string, double>? ParseCharacteristics(string? json) {
        if (string.IsNullOrEmpty(json)) return null;
        try {
            return JsonSerializer.Deserialize<Dictionary<string, double>>(json);
        } catch (JsonException) {
            return null;
        }
    }
}
