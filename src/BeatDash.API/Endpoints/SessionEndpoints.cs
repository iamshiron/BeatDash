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
                    .Select(i => new ScorePointDto(i.SongTimeMs, i.ScoreBefore))
                    .ToListAsync(ct);

                var energy = await db.PlaySessionEnergyChangeItems
                    .AsNoTracking()
                    .Where(i => i.PlaySessionId == id)
                    .OrderBy(i => i.SongTimeMs)
                    .Select(i => new EnergyPointDto(i.SongTimeMs, i.EnergyBefore))
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
                        (int) i.CutDirection,
                        i.LineIndex,
                        i.NoteLineLayer,
                        i.Result,
                        i.MaxScore,
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
    }
}

public sealed record PlaySessionQueryParams(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    BeatmapDifficultyRank? Difficulty = null,
    DateTime? From = null,
    DateTime? To = null,
    Guid? BeatmapId = null,
    SessionSortBy SortBy = SessionSortBy.StartedAt,
    SortDirection SortDir = SortDirection.Desc
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
        PlaySessionResultsDto.From(s.Results)
    );
}

public sealed record PlaySessionDetailDto(
    Guid Id,
    DateTime StartedAt,
    DateTime? EndedAt,
    TimeSpan? Duration,
    BeatmapInfoDto Beatmap,
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
    int MaxPossibleScore,
    float Accuracy,
    string Rank,
    bool FullCombo,
    int MaxCombo,
    int GoodCuts,
    int BadCuts,
    int Misses,
    float FinalEnergy
) {
    internal static PlaySessionResultsDto? From(PlaySessionResults? r) => r is null ? null : new(
        r.Score, r.MaxPossibleScore, r.Accuracy, r.Rank,
        r.FullCombo, r.MaxCombo, r.GoodCuts, r.BadCuts,
        r.Misses, r.FinalEnergy
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
    int CutDirection,
    int LineIndex,
    int NoteLineLayer,
    int Result,
    int MaxScore,
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
