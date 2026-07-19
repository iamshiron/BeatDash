using Microsoft.EntityFrameworkCore;
using Shiron.BeatDash.API.Endpoints;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.API.Services.Health;

/// <summary>
/// Computes a user's fitness aggregates and per-play workout figures from the motion/gameplay
/// data already stored, gated on the user opting in and having entered a weight. Calorie tiers
/// are handled by <see cref="CalorieEstimator"/>; heart-rate enrichment is layered on later.
/// </summary>
public interface IHealthService {
    /// <summary>Career/weekly/today fitness overview, or null when tracking is off or no weight is set.</summary>
    Task<HealthOverviewDto?> GetOverviewAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Per-play workout figures, or null when tracking is off, no weight, or the play isn't the user's.</summary>
    Task<WorkoutDto?> GetWorkoutAsync(Guid userId, Guid sessionId, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class HealthService(BeatDashDbContext db) : IHealthService {
    private const int TrendWeeks = 12;
    private const int HeatmapDays = 182;

    /// <inheritdoc />
    public async Task<HealthOverviewDto?> GetOverviewAsync(Guid userId, CancellationToken ct = default) {
        var user = await LoadProfileAsync(userId, ct);
        if (user is null || !user.HealthTrackingEnabled || user.WeightKg is not { } weightKg)
            return null;

        var age = Age(user.BirthYear);
        var bmi = BodyMetrics.Bmi(user.HeightCm, weightKg);
        var bmr = BodyMetrics.Bmr(weightKg, user.HeightCm, age, user.Sex, user.BodyFatPercent);
        var leanMass = BodyMetrics.LeanMassKg(weightKg, user.BodyFatPercent);

        var recentAvgHr = await db.HeartRateSamples
            .AsNoTracking()
            .Where(h => h.UserId == userId && h.RecordedAt >= DateTime.UtcNow.AddDays(-7))
            .Select(h => (double?) h.Bpm)
            .AverageAsync(ct);

        var plays = await db.PlaySessions
            .AsNoTracking()
            .Where(BaseFilter(userId))
            .Select(s => new PlayRow(
                s.Id, s.StartedAt, s.Results!.EndSongTimeMs, s.BeatmapDifficulty.NotesPerSecond))
            .ToListAsync(ct);

        if (plays.Count == 0) {
            return new HealthOverviewDto(
                0, 0, 0, 0, 0, 0, 0, 0, 0,
                bmi, bmr, leanMass, user.RestingHeartRate, recentAvgHr, [], []);
        }

        var motion = await LoadMotionAsync(plays.Select(p => p.Id).ToList(), ct);

        var today = DateTime.UtcNow.Date;
        var weekStart = MondayOf(today);
        var heatmapSince = today.AddDays(-HeatmapDays);
        var trendStart = weekStart.AddDays(-(TrendWeeks - 1) * 7);

        double careerKcal = 0, careerMinutes = 0, travelMetres = 0;
        double todayKcal = 0, todayMinutes = 0, weekKcal = 0, weekMinutes = 0;
        var perDay = new Dictionary<DateOnly, DailyTotals>();
        var perWeek = new Dictionary<DateOnly, DailyTotals>();

        foreach (var p in plays) {
            motion.TryGetValue(p.Id, out var mo);
            var est = CalorieEstimator.Estimate(
                weightKg, p.EndSongTimeMs, AvgSpeed(mo), mo?.HeadTravel, p.Nps,
                avgHr: null, age, user.Sex);

            careerKcal += est.Kcal;
            careerMinutes += est.ActiveMinutes;
            if (mo is not null) travelMetres += mo.LeftSaberTravel + mo.RightSaberTravel;

            var date = p.StartedAt.Date;
            if (date == today) { todayKcal += est.Kcal; todayMinutes += est.ActiveMinutes; }
            if (date >= weekStart) { weekKcal += est.Kcal; weekMinutes += est.ActiveMinutes; }
            if (date >= heatmapSince) Accumulate(perDay, DateOnly.FromDateTime(date), est);
            if (date >= trendStart) Accumulate(perWeek, DateOnly.FromDateTime(MondayOf(date)), est);
        }

        var activityDays = perDay
            .OrderBy(kv => kv.Key)
            .Select(kv => new HealthDayDto(kv.Key, kv.Value.Kcal, kv.Value.Minutes))
            .ToList();

        var trend = Enumerable.Range(0, TrendWeeks)
            .Select(i => {
                var wk = DateOnly.FromDateTime(trendStart.AddDays(i * 7));
                var v = perWeek.GetValueOrDefault(wk);
                return new HealthTrendWeekDto(wk, v.Kcal, v.Minutes);
            })
            .ToList();

        return new HealthOverviewDto(
            careerKcal, careerMinutes, travelMetres / 1000.0, plays.Count,
            todayKcal, todayMinutes, weekKcal, weekMinutes, careerKcal / plays.Count,
            bmi, bmr, leanMass, user.RestingHeartRate, recentAvgHr,
            activityDays, trend);
    }

    /// <inheritdoc />
    public async Task<WorkoutDto?> GetWorkoutAsync(Guid userId, Guid sessionId, CancellationToken ct = default) {
        var user = await LoadProfileAsync(userId, ct);
        if (user is null || !user.HealthTrackingEnabled || user.WeightKg is not { } weightKg)
            return null;

        var play = await db.PlaySessions
            .AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Where(BaseFilter(userId))
            .Select(s => new {
                s.StartedAt, s.EndedAt, s.Results!.EndSongTimeMs,
                Nps = s.BeatmapDifficulty.NotesPerSecond
            })
            .FirstOrDefaultAsync(ct);
        if (play is null) return null;

        var windowEnd = play.EndedAt ?? play.StartedAt.AddMilliseconds(play.EndSongTimeMs);
        var (avgHr, maxHr) = await HeartRateInWindowAsync(userId, play.StartedAt, windowEnd, ct);

        var mo = (await LoadMotionAsync([sessionId], ct)).GetValueOrDefault(sessionId);

        var est = CalorieEstimator.Estimate(
            weightKg, play.EndSongTimeMs, AvgSpeed(mo), mo?.HeadTravel, play.Nps,
            avgHr, Age(user.BirthYear), user.Sex);

        return new WorkoutDto(
            est.Kcal, est.ActiveMinutes, est.Intensity01, est.Met, Confidence(est.Confidence),
            mo?.LeftSaberTravel ?? 0, mo?.RightSaberTravel ?? 0,
            AvgHeartRate: est.AvgHr, MaxHeartRate: maxHr);
    }

    private async Task<(double? Avg, int? Max)> HeartRateInWindowAsync(
        Guid userId, DateTime start, DateTime end, CancellationToken ct) {
        var bpms = await db.HeartRateSamples
            .AsNoTracking()
            .Where(h => h.UserId == userId && h.RecordedAt >= start && h.RecordedAt <= end)
            .Select(h => h.Bpm)
            .ToListAsync(ct);
        return bpms.Count == 0 ? (null, null) : (bpms.Average(), bpms.Max());
    }

    // --- helpers ---

    private Task<HealthProfile?> LoadProfileAsync(Guid userId, CancellationToken ct) =>
        db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new HealthProfile(
                u.HealthTrackingEnabled, u.WeightKg, u.HeightCm, u.BirthYear,
                u.Sex, u.BodyFatPercent, u.RestingHeartRate))
            .FirstOrDefaultAsync(ct);

    private async Task<Dictionary<Guid, MotionRow>> LoadMotionAsync(List<Guid> playIds, CancellationToken ct) =>
        await db.PlaySessionMotionSummaries
            .AsNoTracking()
            .Where(m => playIds.Contains(m.PlaySessionId))
            .Select(m => new MotionRow(
                m.PlaySessionId, m.AvgLeftSaberSpeed, m.AvgRightSaberSpeed,
                m.HeadTravel, m.LeftSaberTravel, m.RightSaberTravel))
            .ToDictionaryAsync(m => m.PlaySessionId, ct);

    // Any non-auto play with results counts toward energy — failed/quit attempts are still
    // real effort. (Finished-only stats live in ProfileStatsService, not here.)
    private static System.Linq.Expressions.Expression<Func<PlaySession, bool>> BaseFilter(Guid userId) =>
        s => s.UserId == userId
            && !s.AutoMode
            && s.Results != null;

    private static double? AvgSpeed(MotionRow? mo) =>
        mo is null ? null : (mo.AvgLeftSaberSpeed + mo.AvgRightSaberSpeed) / 2;

    private static void Accumulate(Dictionary<DateOnly, DailyTotals> map, DateOnly key, CalorieEstimate est) {
        var cur = map.GetValueOrDefault(key);
        map[key] = new DailyTotals(cur.Kcal + est.Kcal, cur.Minutes + est.ActiveMinutes);
    }

    private static int? Age(int? birthYear) => birthYear is { } by ? DateTime.UtcNow.Year - by : null;

    private static DateTime MondayOf(DateTime date) => date.Date.AddDays(-((int) date.DayOfWeek + 6) % 7);

    private static string Confidence(CalorieConfidence c) => c switch {
        CalorieConfidence.Hr => "hr",
        CalorieConfidence.Motion => "motion",
        _ => "estimated"
    };

    private readonly record struct PlayRow(Guid Id, DateTime StartedAt, int EndSongTimeMs, float Nps);
    private readonly record struct DailyTotals(double Kcal, double Minutes);
    private sealed record MotionRow(
        Guid PlaySessionId, double AvgLeftSaberSpeed, double AvgRightSaberSpeed,
        double HeadTravel, double LeftSaberTravel, double RightSaberTravel);
    private sealed record HealthProfile(
        bool HealthTrackingEnabled, double? WeightKg, int? HeightCm, int? BirthYear,
        string? Sex, double? BodyFatPercent, int? RestingHeartRate);
}
