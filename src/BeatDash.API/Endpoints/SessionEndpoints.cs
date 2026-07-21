using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shiron.BeatDash.API.Services;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.API.Endpoints;

public static class SessionEndpoints {
    public static void MapSessionEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup("/sessions").WithTags("Sessions");

        group.MapGet("/", async (
            [AsParameters] PlaySessionQueryParams queryParams,
            ClaimsPrincipal user,
            BeatDashDbContext db,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();

                var page = Math.Max(1, queryParams.Page);
                var pageSize = Math.Clamp(queryParams.PageSize, 1, 100);

                IQueryable<PlaySession> query = db.PlaySessions
                    .AsNoTracking()
                    .Where(s => s.UserId == userId.Value);

                if (!string.IsNullOrWhiteSpace(queryParams.Search)) {
                    var pattern = $"%{queryParams.Search.Trim()}%";
                    query = query.Where(s =>
                        EF.Functions.ILike(s.BeatmapDifficulty.Beatmap.SongName, pattern) ||
                        EF.Functions.ILike(s.BeatmapDifficulty.Beatmap.SongAuthor, pattern) ||
                        EF.Functions.ILike(s.BeatmapDifficulty.Beatmap.Mapper, pattern));
                }

                if (queryParams.Difficulty.HasValue) {
                    query = query.Where(s => s.BeatmapDifficulty.DifficultyRank == queryParams.Difficulty.Value);
                }

                if (queryParams.From.HasValue) {
                    query = query.Where(s => s.StartedAt >= queryParams.From.Value);
                }

                if (queryParams.To.HasValue) {
                    query = query.Where(s => s.StartedAt <= queryParams.To.Value);
                }

                if (queryParams.BeatmapId.HasValue) {
                    query = query.Where(s => s.BeatmapDifficulty.BeatmapId == queryParams.BeatmapId.Value);
                }

                if (!queryParams.IncludeAuto) {
                    query = query.Where(s => !s.AutoMode);
                }

                query = query.Where(s =>
                    s.EndReason == PlaySessionEndReason.Finished ||
                    (queryParams.IncludeFailed && s.EndReason == PlaySessionEndReason.Failed) ||
                    (queryParams.IncludeQuit && s.EndReason == PlaySessionEndReason.Quit) ||
                    (queryParams.IncludeIncomplete &&
                        (s.EndReason == PlaySessionEndReason.Incomplete || s.EndReason == null)));

                // Result-based filters. Each guards on Results != null so sessions without
                // recorded results (e.g. in-progress) are excluded rather than matched.
                if (queryParams.MinAccuracy.HasValue) {
                    query = query.Where(s => s.Results != null && s.Results.Accuracy >= queryParams.MinAccuracy.Value);
                }

                if (queryParams.MaxAccuracy.HasValue) {
                    query = query.Where(s => s.Results != null && s.Results.Accuracy <= queryParams.MaxAccuracy.Value);
                }

                if (queryParams.MinScore.HasValue) {
                    query = query.Where(s => s.Results != null && s.Results.Score >= queryParams.MinScore.Value);
                }

                if (queryParams.MaxScore.HasValue) {
                    query = query.Where(s => s.Results != null && s.Results.Score <= queryParams.MaxScore.Value);
                }

                if (!string.IsNullOrWhiteSpace(queryParams.Rank)) {
                    query = query.Where(s => s.Results != null && s.Results.Rank == queryParams.Rank);
                }

                if (queryParams.FullComboOnly) {
                    query = query.Where(s => s.Results != null && s.Results.FullCombo);
                }

                if (queryParams.MinBpm.HasValue) {
                    query = query.Where(s => s.BeatmapDifficulty.Beatmap.Bpm >= queryParams.MinBpm.Value);
                }

                if (queryParams.MaxBpm.HasValue) {
                    query = query.Where(s => s.BeatmapDifficulty.Beatmap.Bpm <= queryParams.MaxBpm.Value);
                }

                if (queryParams.SortBy is SessionSortBy.Score or SessionSortBy.Accuracy
                    or SessionSortBy.MaxCombo or SessionSortBy.Duration) {
                    query = query.Where(s => s.EndedAt != null);
                }

                query = (queryParams.SortBy, queryParams.SortDir) switch {
                    (SessionSortBy.StartedAt, SortDirection.Asc) => query.OrderBy(s => s.StartedAt),
                    (SessionSortBy.StartedAt, _) => query.OrderByDescending(s => s.StartedAt),
                    (SessionSortBy.Score, SortDirection.Asc) => query.OrderBy(s => s.Results!.Score),
                    (SessionSortBy.Score, _) => query.OrderByDescending(s => s.Results!.Score),
                    (SessionSortBy.Accuracy, SortDirection.Asc) => query.OrderBy(s => s.Results!.Accuracy),
                    (SessionSortBy.Accuracy, _) => query.OrderByDescending(s => s.Results!.Accuracy),
                    (SessionSortBy.MaxCombo, SortDirection.Asc) => query.OrderBy(s => s.Results!.MaxCombo),
                    (SessionSortBy.MaxCombo, _) => query.OrderByDescending(s => s.Results!.MaxCombo),
                    (SessionSortBy.Duration, SortDirection.Asc) => query.OrderBy(s => s.EndedAt!.Value - s.StartedAt),
                    (SessionSortBy.Duration, _) => query.OrderByDescending(s => s.EndedAt!.Value - s.StartedAt),
                    _ => query.OrderByDescending(s => s.StartedAt),
                };

                var totalCount = await query.CountAsync(ct);
                var totalPages = totalCount == 0 ? 0 : (int) Math.Ceiling(totalCount / (double) pageSize);

                var sessions = await query
                    .Include(s => s.BeatmapDifficulty.Beatmap)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(ct);

                // Best completed non-auto score per difficulty (for the page's difficulties),
                // used to flag which sessions are personal bests.
                var pageDifficultyIds = sessions.Select(s => s.BeatmapDifficultyId).Distinct().ToList();
                var bestScores = await db.PlaySessions
                    .AsNoTracking()
                    .Where(s =>
                        s.UserId == userId.Value &&
                        !s.AutoMode &&
                        s.EndReason == PlaySessionEndReason.Finished &&
                        s.Results != null &&
                        pageDifficultyIds.Contains(s.BeatmapDifficultyId))
                    .GroupBy(s => s.BeatmapDifficultyId)
                    .Select(g => new { DifficultyId = g.Key, MaxScore = g.Max(x => x.Results!.Score) })
                    .ToDictionaryAsync(x => x.DifficultyId, x => x.MaxScore, ct);

                var items = sessions.Select(s => PlaySessionListItemDto.From(s, IsPersonalBest(s, bestScores))).ToList();

                return Results.Ok(new PagedResult<PlaySessionListItemDto>(items, totalCount, page, pageSize, totalPages));
            })
            .RequireAuthorization()
            .Produces<PagedResult<PlaySessionListItemDto>>()
            .Produces(401);

        group.MapGet("/{id:Guid}", async (
            Guid id,
            ClaimsPrincipal user,
            BeatDashDbContext db,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();

                var session = await db.PlaySessions
                    .AsNoTracking()
                    .Include(s => s.BeatmapDifficulty.Beatmap)
                    .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId.Value, ct);

                if (session is null) return Results.NotFound();

                var noteCount = await db.PlaySessionNoteItems.CountAsync(i => i.PlaySessionId == id, ct);
                var comboBreakCount = await db.PlaySessionComboBreakItems.CountAsync(i => i.PlaySessionId == id, ct);
                var hasMotionData = await db.PlaySessionItemMotionFrames.AnyAsync(i => i.PlaySessionId == id, ct);
                var hasMotionSummary = await db.PlaySessionMotionSummaries.AnyAsync(i => i.PlaySessionId == id, ct);

                return Results.Ok(PlaySessionDetailDto.From(session, noteCount, comboBreakCount, hasMotionData, hasMotionSummary));
            })
            .RequireAuthorization()
            .Produces<PlaySessionDetailDto>()
            .Produces(404)
            .Produces(401);

        group.MapGet("/{id:Guid}/timeline", async (
            Guid id,
            ClaimsPrincipal user,
            BeatDashDbContext db,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();

                var exists = await db.PlaySessions
                    .AnyAsync(s => s.Id == id && s.UserId == userId.Value, ct);
                if (!exists) return Results.NotFound();

                var score = await db.PlaySessionScoreChangeItems
                    .AsNoTracking()
                    .Where(i => i.PlaySessionId == id)
                    .OrderBy(i => i.SongTimeMs)
                    .Select(i => new ScorePointDto(i.SongTimeMs, i.Score))
                    .ToListAsync(ct);

                var energy = await db.PlaySessionEnergyChangeItems
                    .AsNoTracking()
                    .Where(i => i.PlaySessionId == id)
                    .OrderBy(i => i.SongTimeMs)
                    .Select(i => new EnergyPointDto(i.SongTimeMs, i.Energy))
                    .ToListAsync(ct);

                var comboBreaks = await db.PlaySessionComboBreakItems
                    .AsNoTracking()
                    .Where(i => i.PlaySessionId == id)
                    .OrderBy(i => i.SongTimeMs)
                    .Select(i => new ComboBreakPointDto(i.SongTimeMs, i.ComboBefore))
                    .ToListAsync(ct);

                return Results.Ok(new PlaySessionTimelineDto(score, energy, comboBreaks));
            })
            .RequireAuthorization()
            .Produces<PlaySessionTimelineDto>()
            .Produces(404)
            .Produces(401);

        group.MapGet("/{id:Guid}/notes", async (
            Guid id,
            ClaimsPrincipal user,
            BeatDashDbContext db,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();

                var exists = await db.PlaySessions
                    .AnyAsync(s => s.Id == id && s.UserId == userId.Value, ct);
                if (!exists) return Results.NotFound();

                var notes = await db.PlaySessionNoteItems
                    .AsNoTracking()
                    .Where(i => i.PlaySessionId == id)
                    .OrderBy(i => i.SongTimeMs)
                    .Select(i => new NoteItemDto(
                        i.SongTimeMs,
                        (int) i.ColorType,
                        (int) i.NoteType,
                        (int) i.ScoringType,
                        (int) i.CutDirection,
                        i.LineIndex,
                        i.NoteLineLayer,
                        i.Result,
                        i.MaxScore,
                        i.BeforeCutScore,
                        i.CenterDistanceScore,
                        i.AfterCutScore,
                        i.PreCutSwing,
                        i.PostCutSwing,
                        i.CutPointDistance,
                        i.SaberSpeed))
                    .ToListAsync(ct);

                return Results.Ok(notes);
            })
            .RequireAuthorization()
            .Produces<IList<NoteItemDto>>()
            .Produces(404)
            .Produces(401);

        group.MapGet("/{id:Guid}/motion", async (
            Guid id,
            ClaimsPrincipal user,
            BeatDashDbContext db,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();

                var exists = await db.PlaySessions
                    .AnyAsync(s => s.Id == id && s.UserId == userId.Value, ct);
                if (!exists) return Results.NotFound();

                var frames = await db.PlaySessionItemMotionFrames
                    .AsNoTracking()
                    .Where(i => i.PlaySessionId == id)
                    .OrderBy(i => i.SongTimeMs)
                    .Select(i => new { i.SongTimeMs, i.FrameCount, i.Data })
                    .ToListAsync(ct);

                if (frames.Count == 0) return Results.NotFound();

                var segments = frames
                    .Select(f => new MotionSegmentDto(f.SongTimeMs, f.FrameCount, Convert.ToBase64String(f.Data)))
                    .ToList();

                var totalFrames = segments.Sum(s => s.FrameCount);
                return Results.Ok(new PlaySessionMotionDto(30, totalFrames, segments));
            })
            .RequireAuthorization()
            .Produces<PlaySessionMotionDto>()
            .Produces(404)
            .Produces(401);

        group.MapGet("/{id:Guid}/motion-summary", async (
            Guid id,
            ClaimsPrincipal user,
            BeatDashDbContext db,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();

                var owns = await db.PlaySessions.AnyAsync(s => s.Id == id && s.UserId == userId.Value, ct);
                if (!owns) return Results.NotFound();

                var summary = await db.PlaySessionMotionSummaries
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.PlaySessionId == id, ct);
                if (summary is null) return Results.NotFound();

                var fatigue = DeserializeFatigue(summary.FatigueCurve);
                return Results.Ok(new MotionSummaryDto(
                    summary.FrameCount,
                    summary.SampleRateHz,
                    summary.LeftSaberTravel,
                    summary.RightSaberTravel,
                    summary.HeadTravel,
                    summary.AvgLeftSaberSpeed,
                    summary.AvgRightSaberSpeed,
                    summary.LeftReachRange,
                    summary.RightReachRange,
                    summary.HeadRange,
                    fatigue));
            })
            .RequireAuthorization()
            .Produces<MotionSummaryDto>()
            .Produces(404)
            .Produces(401);

        group.MapGet("/stats", async (
            ClaimsPrincipal user,
            IProfileStatsService profileStats,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();

                return Results.Ok(await profileStats.GetStatsAsync(userId.Value, ct));
            })
            .RequireAuthorization()
            .Produces<UserStatsDto>()
            .Produces(401);

        group.MapGet("/skill", async (
            ClaimsPrincipal user,
            IProfileStatsService profileStats,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();

                return Results.Ok(await profileStats.GetSkillAsync(userId.Value, ct));
            })
            .RequireAuthorization()
            .Produces<SkillProfileDto>()
            .Produces(401);

        group.MapGet("/latest-summary", async (
            ClaimsPrincipal user,
            IProfileStatsService profileStats,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();

                var summary = await profileStats.GetLatestSessionSummaryAsync(userId.Value, ct);
                return summary is null ? Results.NoContent() : Results.Ok(summary);
            })
            .RequireAuthorization()
            .Produces<SessionSummaryDto>()
            .Produces(204)
            .Produces(401);

        // Paginated list of the user's sittings (grouped plays), newest first.
        group.MapGet("/sittings", async (
            int? page,
            int? pageSize,
            SittingSortBy? sortBy,
            ClaimsPrincipal user,
            IProfileStatsService profileStats,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();

                var result = await profileStats.GetSittingsAsync(
                    userId.Value, page ?? 1, pageSize ?? 20, sortBy ?? SittingSortBy.Newest, ct);
                return Results.Ok(result);
            })
            .WithName("GetSittings")
            .WithDescription("The user's sessions (sittings of plays), ordered by sortBy, paginated.")
            .RequireAuthorization()
            .Produces<PagedResult<SessionSummaryDto>>()
            .Produces(401);

        group.MapGet("/sittings/overview", async (
            ClaimsPrincipal user,
            IProfileStatsService profileStats,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();

                return Results.Ok(await profileStats.GetSittingsOverviewAsync(userId.Value, ct));
            })
            .WithName("GetSittingsOverview")
            .WithDescription("At-a-glance totals across every one of the user's sessions.")
            .RequireAuthorization()
            .Produces<SittingsOverviewDto>()
            .Produces(401);

        group.MapGet("/recommendations", async (
            int? limit,
            ClaimsPrincipal user,
            IPracticeRecommendationService recommendations,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();

                return Results.Ok(await recommendations.GetRecommendationsAsync(userId.Value, limit ?? 10, ct));
            })
            .RequireAuthorization()
            .Produces<IList<PracticeRecommendationDto>>()
            .Produces(401);

        group.MapGet("/skill/progression", async (
            int? weeks,
            ClaimsPrincipal user,
            IProfileStatsService profileStats,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();

                return Results.Ok(await profileStats.GetSkillProgressionAsync(userId.Value, weeks ?? 12, ct));
            })
            .RequireAuthorization()
            .Produces<SkillProgressionDto>()
            .Produces(401);

        group.MapGet("/weakness", async (
            string? characteristic,
            ClaimsPrincipal user,
            IProfileStatsService profileStats,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();

                return Results.Ok(await profileStats.GetWeaknessAsync(userId.Value, characteristic, ct));
            })
            .RequireAuthorization()
            .Produces<WeaknessProfileDto>()
            .Produces(401);

        group.MapGet("/trends", async (
            ClaimsPrincipal user,
            BeatDashDbContext db,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();

                const int weeks = 12;
                var today = DateTime.UtcNow.Date;
                // Monday of the current week, then back (weeks - 1) weeks for the window start.
                var weekStart = today.AddDays(-((int) today.DayOfWeek + 6) % 7);
                var windowStart = weekStart.AddDays(-(weeks - 1) * 7);

                var daily = await db.PlaySessions
                    .AsNoTracking()
                    .Where(s =>
                        s.UserId == userId.Value &&
                        !s.AutoMode &&
                        s.EndReason == PlaySessionEndReason.Finished &&
                        s.Results != null &&
                        s.StartedAt >= windowStart)
                    .GroupBy(s => s.StartedAt.Date)
                    .Select(g => new {
                        Day = g.Key,
                        Plays = g.Count(),
                        AccuracySum = g.Sum(s => (double) s.Results!.Accuracy),
                        TimeMs = g.Sum(s => (long) s.Results!.EndSongTimeMs)
                    })
                    .ToListAsync(ct);

                // Fold daily aggregates into fixed weekly buckets across the window.
                var buckets = new List<TrendBucketDto>(weeks);
                for (var i = 0; i < weeks; i++) {
                    var bucketStart = windowStart.AddDays(i * 7);
                    var bucketEnd = bucketStart.AddDays(7);
                    var days = daily.Where(d => d.Day >= bucketStart && d.Day < bucketEnd).ToList();
                    var plays = days.Sum(d => d.Plays);
                    buckets.Add(new TrendBucketDto(
                        DateOnly.FromDateTime(bucketStart),
                        plays,
                        plays > 0 ? days.Sum(d => d.AccuracySum) / plays : null,
                        days.Sum(d => d.TimeMs)));
                }

                return Results.Ok(buckets);
            })
            .RequireAuthorization()
            .Produces<IList<TrendBucketDto>>()
            .Produces(401);

        group.MapGet("/pb", async (
            Guid mapId,
            string difficulty,
            string characteristic,
            ClaimsPrincipal user,
            BeatDashDbContext db,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();
                if (!Enum.TryParse<BeatmapDifficultyRank>(difficulty, out var rank))
                    return Results.BadRequest("Invalid difficulty rank.");

                // Best completed non-auto score on this exact difficulty variant.
                var best = await db.PlaySessions
                    .AsNoTracking()
                    .Where(s =>
                        s.UserId == userId.Value &&
                        !s.AutoMode &&
                        s.EndReason == PlaySessionEndReason.Finished &&
                        s.Results != null &&
                        s.BeatmapDifficulty.BeatmapId == mapId &&
                        s.BeatmapDifficulty.DifficultyRank == rank &&
                        s.BeatmapDifficulty.CharacteristicSerializedName == characteristic)
                    .OrderByDescending(s => s.Results!.Score)
                    .Select(s => new PersonalBestDto(
                        s.Id, s.Results!.Score, s.Results.Accuracy, s.Results.Rank))
                    .FirstOrDefaultAsync(ct);

                return Results.Ok(best);
            })
            .RequireAuthorization()
            .Produces<PersonalBestDto>()
            .Produces(400)
            .Produces(401);

        group.MapGet("/{id:Guid}/recap", async (
            Guid id,
            ClaimsPrincipal user,
            IProfileStatsService profileStats,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();

                var recap = await profileStats.GetRecapAsync(userId.Value, id, ct);
                return recap is null ? Results.NotFound() : Results.Ok(recap);
            })
            .RequireAuthorization()
            .Produces<SessionRecapDto>()
            .Produces(404)
            .Produces(401);

        group.MapGet("/{id:Guid}/top", async (
            Guid id,
            ClaimsPrincipal user,
            BeatDashDbContext db,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();

                var current = await db.PlaySessions
                    .AsNoTracking()
                    .Where(s => s.Id == id && s.UserId == userId.Value)
                    .Select(s => new {
                        s.BeatmapDifficultyId,
                        s.BeatmapDifficulty.BeatmapId,
                        s.BeatmapDifficulty.CharacteristicSerializedName
                    })
                    .FirstOrDefaultAsync(ct);

                if (current is null) return Results.NotFound();

                // All difficulties of the same map + characteristic, in ascending rank order.
                var difficulties = await db.BeatmapDifficulties
                    .AsNoTracking()
                    .Where(d =>
                        d.BeatmapId == current.BeatmapId &&
                        d.CharacteristicSerializedName == current.CharacteristicSerializedName)
                    .OrderBy(d => d.DifficultyRank)
                    .Select(d => new { d.Id, d.DifficultyRank, d.DifficultyName })
                    .ToListAsync(ct);

                var difficultyIds = difficulties.Select(d => d.Id).ToList();

                var sessions = await db.PlaySessions
                    .AsNoTracking()
                    .Where(s =>
                        s.UserId == userId.Value &&
                        difficultyIds.Contains(s.BeatmapDifficultyId) &&
                        !s.AutoMode &&
                        s.EndReason == PlaySessionEndReason.Finished &&
                        s.Results != null)
                    .Include(s => s.BeatmapDifficulty.Beatmap)
                    .OrderByDescending(s => s.Results!.Score)
                    .ToListAsync(ct);

                var result = difficulties.Select(d => new SessionTopDifficultyDto(
                    d.Id,
                    d.DifficultyRank.ToString(),
                    d.DifficultyName,
                    d.Id == current.BeatmapDifficultyId,
                    sessions
                        .Where(s => s.BeatmapDifficultyId == d.Id)
                        .Take(10)
                        .Select(s => PlaySessionListItemDto.From(s))
                        .ToList()
                )).ToList();

                return Results.Ok(result);
            })
            .RequireAuthorization()
            .Produces<IList<SessionTopDifficultyDto>>()
            .Produces(404)
            .Produces(401);
    }

    private static readonly JsonSerializerOptions FatigueJsonOptions = new() { PropertyNameCaseInsensitive = true };

    // Maps the stored fatigue JSON ({tMs,leftSpeed,rightSpeed}) to the response DTO.
    private static IList<FatiguePointDto> DeserializeFatigue(string json) {
        if (string.IsNullOrEmpty(json)) return [];
        try {
            var raw = JsonSerializer.Deserialize<List<FatigueRow>>(json, FatigueJsonOptions);
            return raw is null
                ? []
                : raw.Select(r => new FatiguePointDto(r.TMs, r.LeftSpeed, r.RightSpeed)).ToList();
        } catch (JsonException) {
            return [];
        }
    }

    private sealed record FatigueRow(int TMs, double LeftSpeed, double RightSpeed);

    // A completed non-auto session is a personal best when its score matches the
    // top score recorded on its difficulty.
    private static bool IsPersonalBest(PlaySession s, IReadOnlyDictionary<Guid, int> bestScores) =>
        !s.AutoMode &&
        s.EndReason == PlaySessionEndReason.Finished &&
        s.Results != null &&
        bestScores.TryGetValue(s.BeatmapDifficultyId, out var max) &&
        s.Results.Score == max;
}

public sealed record UserStatsDto(
    int TotalPlays,
    long TotalPlayTimeMs,
    float AverageAccuracy,
    int FullCombos,
    int UniqueMaps,
    IList<RankCountDto> RankDistribution,
    IList<ActivityDayDto> Activity,
    IList<MostPlayedMapDto> MostPlayedMaps,
    IList<PlaySessionListItemDto> RecentSessions,
    IList<PlaySessionListItemDto> TopScores
);

public sealed record RankCountDto(string Rank, int Count);
public sealed record ActivityDayDto(DateOnly Date, int Count);

public sealed record SkillProfileDto(
    IList<SkillCharacteristicDto> Characteristics,
    int PlaysConsidered
);

/// <summary>Weekly skill-profile time series. <see cref="Characteristics"/> lists the union of axes seen across all weeks.</summary>
public sealed record SkillProgressionDto(
    IList<SkillProgressionWeekDto> Weeks,
    IList<string> Characteristics
);

public sealed record SkillProgressionWeekDto(
    DateOnly WeekStart,
    IList<SkillCharacteristicDto> Characteristics,
    int PlaysConsidered
);

/// <summary>
/// One characteristic axis of a player's skill profile. <see cref="Skill"/> weights
/// map intensity by the accuracy achieved; <see cref="Exposure"/> is the raw average
/// intensity of the maps played (both in <c>[0,1]</c>).
/// </summary>
public sealed record SkillCharacteristicDto(string Key, double Skill, double Exposure);

public sealed record TrendBucketDto(
    DateOnly WeekStart,
    int Plays,
    double? AvgAccuracy,
    long PlayTimeMs
);

/// <summary>The user's best completed score on a specific difficulty variant.</summary>
public sealed record PersonalBestDto(Guid SessionId, int Score, float Accuracy, string Rank);
public sealed record MostPlayedMapDto(
    Guid BeatmapId,
    string SongName,
    string SongAuthor,
    string Mapper,
    int PlayCount
);

public sealed record SessionTopDifficultyDto(
    Guid BeatmapDifficultyId,
    string DifficultyRank,
    string DifficultyName,
    bool IsCurrent,
    IList<PlaySessionListItemDto> Sessions
);

public sealed record PlaySessionQueryParams(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    BeatmapDifficultyRank? Difficulty = null,
    DateTime? From = null,
    DateTime? To = null,
    Guid? BeatmapId = null,
    SessionSortBy SortBy = SessionSortBy.StartedAt,
    SortDirection SortDir = SortDirection.Desc,
    bool IncludeAuto = false,
    bool IncludeFailed = false,
    bool IncludeQuit = false,
    bool IncludeIncomplete = false,
    float? MinAccuracy = null,
    float? MaxAccuracy = null,
    int? MinScore = null,
    int? MaxScore = null,
    string? Rank = null,
    bool FullComboOnly = false,
    float? MinBpm = null,
    float? MaxBpm = null
);

public enum SessionSortBy { StartedAt, Score, Accuracy, Duration, MaxCombo }
public enum SortDirection { Asc, Desc }

/// <summary>Ordering for the sessions (sittings) list. Keys are computable from the play timeline alone.</summary>
public enum SittingSortBy { Newest, Oldest, MostPlays, Longest }

public sealed record PlaySessionListItemDto(
    Guid Id,
    DateTime StartedAt,
    DateTime? EndedAt,
    TimeSpan? Duration,
    Guid BeatmapId,
    Guid BeatmapDifficultyId,
    string SongName,
    string? SongSubName,
    string SongAuthor,
    string Mapper,
    float Bpm,
    string DifficultyRank,
    string DifficultyName,
    bool AutoMode,
    string? EndReason,
    int? ModifierFlags,
    PlaySessionResultsDto? Results,
    bool IsPersonalBest
) {
    internal static PlaySessionListItemDto From(PlaySession s, bool isPersonalBest = false) => new(
        s.Id,
        s.StartedAt,
        s.EndedAt,
        s.EndedAt.HasValue ? s.EndedAt.Value - s.StartedAt : null,
        s.BeatmapDifficulty.BeatmapId,
        s.BeatmapDifficultyId,
        s.BeatmapDifficulty.Beatmap.SongName,
        s.BeatmapDifficulty.Beatmap.SongSubName,
        s.BeatmapDifficulty.Beatmap.SongAuthor,
        s.BeatmapDifficulty.Beatmap.Mapper,
        s.BeatmapDifficulty.Beatmap.Bpm,
        s.BeatmapDifficulty.DifficultyRank.ToString(),
        s.BeatmapDifficulty.DifficultyName,
        s.AutoMode,
        s.EndReason?.ToString(),
        s.ModifierFlags,
        PlaySessionResultsDto.From(s.Results),
        isPersonalBest
    );
}

public sealed record PlaySessionDetailDto(
    Guid Id,
    DateTime StartedAt,
    DateTime? EndedAt,
    TimeSpan? Duration,
    BeatmapInfoDto Beatmap,
    bool AutoMode,
    string? EndReason,
    int? ModifierFlags,
    PlaySessionResultsDto? Results,
    int NoteCount,
    int ComboBreakCount,
    bool HasMotionData,
    bool HasMotionSummary
) {
    internal static PlaySessionDetailDto From(
        PlaySession s, int noteCount, int comboBreakCount, bool hasMotionData, bool hasMotionSummary) => new(
        s.Id,
        s.StartedAt,
        s.EndedAt,
        s.EndedAt.HasValue ? s.EndedAt.Value - s.StartedAt : null,
        BeatmapInfoDto.From(s.BeatmapDifficulty),
        s.AutoMode,
        s.EndReason?.ToString(),
        s.ModifierFlags,
        PlaySessionResultsDto.From(s.Results),
        noteCount,
        comboBreakCount,
        hasMotionData,
        hasMotionSummary
    );
}

public sealed record BeatmapInfoDto(
    Guid Id,
    string LevelId,
    string SongName,
    string? SongSubName,
    string SongAuthor,
    string Mapper,
    float Bpm,
    int DurationMs,
    string DifficultyRank,
    string DifficultyName,
    float NotesPerSecond,
    int CuttableObjectCount,
    int BombCount,
    int ObstacleCount,
    int LaneCount,
    float? NoteJumpSpeed,
    string CharacteristicSerializedName
) {
    internal static BeatmapInfoDto From(BeatmapDifficulty d) => new(
        d.Beatmap.Id,
        d.Beatmap.LevelId,
        d.Beatmap.SongName,
        d.Beatmap.SongSubName,
        d.Beatmap.SongAuthor,
        d.Beatmap.Mapper,
        d.Beatmap.Bpm,
        d.Beatmap.DurationMs,
        d.DifficultyRank.ToString(),
        d.DifficultyName,
        d.NotesPerSecond,
        d.CuttableObjectCount,
        d.BombCount,
        d.ObstacleCount,
        d.LaneCount,
        d.NoteJumpSpeed,
        d.CharacteristicSerializedName
    );
}

public sealed record PlaySessionResultsDto(
    int Score,
    int MultipliedScore,
    int MaxPossibleScore,
    float Accuracy,
    string Rank,
    bool FullCombo,
    int MaxCombo,
    int GoodCuts,
    int BadCuts,
    int Misses,
    float FinalEnergy,
    int EndSongTimeMs
) {
    internal static PlaySessionResultsDto? From(PlaySessionResults? r) => r is null ? null : new(
        r.Score, r.MultipliedScore, r.MaxPossibleScore, r.Accuracy, r.Rank,
        r.FullCombo, r.MaxCombo, r.GoodCuts, r.BadCuts,
        r.Misses, r.FinalEnergy, r.EndSongTimeMs
    );
}

public sealed record PlaySessionTimelineDto(
    IList<ScorePointDto> Score,
    IList<EnergyPointDto> Energy,
    IList<ComboBreakPointDto> ComboBreaks
);

public sealed record ScorePointDto(int SongTimeMs, int Score);
public sealed record EnergyPointDto(int SongTimeMs, float Energy);
public sealed record ComboBreakPointDto(int SongTimeMs, int ComboBefore);

public sealed record NoteItemDto(
    int SongTimeMs,
    int ColorType,
    int NoteType,
    int ScoringType,
    int CutDirection,
    int LineIndex,
    int NoteLineLayer,
    int Result,
    int MaxScore,
    int BeforeCutScore,
    int CenterDistanceScore,
    int AfterCutScore,
    float PreCutSwing,
    float PostCutSwing,
    float CutPointDistance,
    float SaberSpeed
);

public sealed record PlaySessionMotionDto(
    int SampleRateHz,
    int TotalFrameCount,
    IList<MotionSegmentDto> Segments
);

public sealed record MotionSegmentDto(
    int StartSongTimeMs,
    int FrameCount,
    string Data
);

/// <summary>Server-computed motion metrics for a play (see <c>MotionSummaryCalculator</c>).</summary>
public sealed record MotionSummaryDto(
    int FrameCount,
    int SampleRateHz,
    double LeftSaberTravel,
    double RightSaberTravel,
    double HeadTravel,
    double AvgLeftSaberSpeed,
    double AvgRightSaberSpeed,
    double LeftReachRange,
    double RightReachRange,
    double HeadRange,
    IList<FatiguePointDto> FatigueCurve
);

/// <summary>One fatigue-curve point: average saber speed (m/s) at a song time.</summary>
public sealed record FatiguePointDto(int SongTimeMs, double LeftSpeed, double RightSpeed);
