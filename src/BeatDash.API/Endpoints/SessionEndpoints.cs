using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
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

                var items = sessions.Select(PlaySessionListItemDto.From).ToList();

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

                return Results.Ok(PlaySessionDetailDto.From(session, noteCount, comboBreakCount, hasMotionData));
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

        group.MapGet("/stats", async (
            ClaimsPrincipal user,
            BeatDashDbContext db,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();

                // Completed, non-auto plays with final results — the basis for all aggregates.
                var baseQuery = db.PlaySessions
                    .AsNoTracking()
                    .Where(s =>
                        s.UserId == userId.Value &&
                        !s.AutoMode &&
                        s.EndReason == PlaySessionEndReason.Finished &&
                        s.Results != null);

                var totalPlays = await baseQuery.CountAsync(ct);
                if (totalPlays == 0) {
                    return Results.Ok(new UserStatsDto(
                        0, 0, 0, 0, 0,
                        [], [], [], [], []));
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

                return Results.Ok(new UserStatsDto(
                    totalPlays,
                    totalPlayTimeMs,
                    averageAccuracy,
                    fullCombos,
                    uniqueMaps,
                    rankDistribution,
                    activity,
                    mostPlayed,
                    recentSessions.Select(PlaySessionListItemDto.From).ToList(),
                    topScores.Select(PlaySessionListItemDto.From).ToList()));
            })
            .RequireAuthorization()
            .Produces<UserStatsDto>()
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
                        .Select(PlaySessionListItemDto.From)
                        .ToList()
                )).ToList();

                return Results.Ok(result);
            })
            .RequireAuthorization()
            .Produces<IList<SessionTopDifficultyDto>>()
            .Produces(404)
            .Produces(401);
    }
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
    bool IncludeIncomplete = false
);

public enum SessionSortBy { StartedAt, Score, Accuracy, Duration, MaxCombo }
public enum SortDirection { Asc, Desc }

public sealed record PlaySessionListItemDto(
    Guid Id,
    DateTime StartedAt,
    DateTime? EndedAt,
    TimeSpan? Duration,
    Guid BeatmapId,
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
    PlaySessionResultsDto? Results
) {
    internal static PlaySessionListItemDto From(PlaySession s) => new(
        s.Id,
        s.StartedAt,
        s.EndedAt,
        s.EndedAt.HasValue ? s.EndedAt.Value - s.StartedAt : null,
        s.BeatmapDifficulty.BeatmapId,
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
        PlaySessionResultsDto.From(s.Results)
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
    bool HasMotionData
) {
    internal static PlaySessionDetailDto From(
        PlaySession s, int noteCount, int comboBreakCount, bool hasMotionData) => new(
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
        hasMotionData
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
