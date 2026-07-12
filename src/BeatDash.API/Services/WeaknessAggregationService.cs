using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.API.Services;

/// <summary>
/// Maintains the per-user <see cref="PlayNoteAggregate"/> rollup that powers the
/// lifetime-weakness views, folding each completed session's notes exactly once.
/// </summary>
public interface IWeaknessAggregationService {
    /// <summary>
    /// Folds a single finished session's notes into the user's aggregate. Idempotent:
    /// a session is claimed via <see cref="PlaySession.AggregatedAt"/> and skipped if
    /// already folded or not eligible.
    /// </summary>
    Task FoldSessionAsync(Guid playSessionId, CancellationToken ct = default);

    /// <summary>
    /// Rebuilds a user's entire aggregate from scratch (delete + regroup all eligible
    /// sessions). Used for backfill and repair — not on the request path.
    /// </summary>
    /// <returns>The number of sessions folded into the rebuilt aggregate.</returns>
    Task<int> RebuildUserAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// <see cref="IDbContextFactory{TContext}"/>-backed implementation so it can run
/// outside a request scope (called fire-and-forget from session finalization).
/// </summary>
public sealed class WeaknessAggregationService(
    IDbContextFactory<BeatDashDbContext> dbFactory,
    ILogger<WeaknessAggregationService> logger
) : IWeaknessAggregationService {

    /// <inheritdoc/>
    public async Task FoldSessionAsync(Guid playSessionId, CancellationToken ct = default) {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Atomically claim the session: only one caller can flip AggregatedAt from null,
        // so concurrent/duplicate folds for the same session are no-ops.
        var claimed = await db.PlaySessions
            .Where(s =>
                s.Id == playSessionId &&
                s.AggregatedAt == null &&
                !s.AutoMode &&
                s.EndReason == PlaySessionEndReason.Finished &&
                s.Results != null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.AggregatedAt, DateTime.UtcNow), ct);

        if (claimed == 0) {
            await tx.RollbackAsync(ct);
            return;
        }

        var meta = await db.PlaySessions
            .Where(s => s.Id == playSessionId)
            .Select(s => new { s.UserId, s.BeatmapDifficulty.CharacteristicSerializedName })
            .FirstAsync(ct);

        var deltas = await ComputeSessionDeltasAsync(db, playSessionId, ct);
        foreach (var d in deltas) {
            await UpsertAsync(db, meta.UserId, meta.CharacteristicSerializedName, d, ct);
        }

        await tx.CommitAsync(ct);
        logger.LogInformation(
            "Folded {Keys} note-aggregate keys for play session {PlaySessionId}",
            deltas.Count, playSessionId);
    }

    /// <inheritdoc/>
    public async Task<int> RebuildUserAsync(Guid userId, CancellationToken ct = default) {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        await db.PlayNoteAggregates.Where(a => a.UserId == userId).ExecuteDeleteAsync(ct);

        var sessions = await db.PlaySessions
            .Where(s =>
                s.UserId == userId &&
                !s.AutoMode &&
                s.EndReason == PlaySessionEndReason.Finished &&
                s.Results != null)
            .Select(s => new { s.Id, s.BeatmapDifficulty.CharacteristicSerializedName })
            .ToListAsync(ct);

        foreach (var session in sessions) {
            var deltas = await ComputeSessionDeltasAsync(db, session.Id, ct);
            foreach (var d in deltas) {
                await UpsertAsync(db, userId, session.CharacteristicSerializedName, d, ct);
            }
        }

        // Mark every folded session so incremental folds don't double-count later.
        await db.PlaySessions
            .Where(s =>
                s.UserId == userId &&
                !s.AutoMode &&
                s.EndReason == PlaySessionEndReason.Finished &&
                s.Results != null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.AggregatedAt, DateTime.UtcNow), ct);

        await tx.CommitAsync(ct);
        logger.LogInformation("Rebuilt note aggregate for user {UserId} from {Sessions} sessions", userId, sessions.Count);
        return sessions.Count;
    }

    /// <summary>
    /// Groups a session's real-direction, non-bomb notes into per-key deltas. Cut
    /// accuracy sums (earned/max) cover good cuts only; miss/bad rows contribute to
    /// the counts but not the score sums.
    /// </summary>
    private static async Task<List<NoteDelta>> ComputeSessionDeltasAsync(
        BeatDashDbContext db, Guid playSessionId, CancellationToken ct) =>
        await db.PlaySessionNoteItems
            .AsNoTracking()
            .Where(n =>
                n.PlaySessionId == playSessionId &&
                n.ColorType != ColorType.None &&
                (int) n.CutDirection <= 7)
            .GroupBy(n => new { n.ColorType, n.CutDirection, n.LineIndex, n.NoteLineLayer })
            .Select(g => new NoteDelta(
                g.Key.ColorType,
                g.Key.CutDirection,
                g.Key.LineIndex,
                g.Key.NoteLineLayer,
                g.LongCount(),
                g.LongCount(x => x.Result == 0),
                g.LongCount(x => x.Result == 2),
                g.LongCount(x => x.Result == 1),
                g.Sum(x => x.Result == 0 ? (long) (x.BeforeCutScore + x.CenterDistanceScore + x.AfterCutScore) : 0L),
                g.Sum(x => x.Result == 0 ? (long) x.MaxScore : 0L)))
            .ToListAsync(ct);

    /// <summary>
    /// Additive Postgres upsert keyed on the unique aggregate index. Concurrent folds
    /// for the same user serialize on the conflicting row and sum correctly.
    /// </summary>
    private static async Task UpsertAsync(
        BeatDashDbContext db, Guid userId, string characteristic, NoteDelta d, CancellationToken ct) {
        var id = Guid.CreateVersion7();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "PlayNoteAggregates"
                ("Id", "UserId", "CharacteristicSerializedName", "ColorType", "CutDirection",
                 "LineIndex", "NoteLineLayer", "NoteCount", "GoodCount", "MissCount", "BadCount",
                 "SumEarnedScore", "SumMaxScore")
            VALUES
                ({id}, {userId}, {characteristic}, {(int) d.ColorType}, {(int) d.CutDirection},
                 {d.LineIndex}, {d.NoteLineLayer}, {d.NoteCount}, {d.GoodCount}, {d.MissCount}, {d.BadCount},
                 {d.SumEarnedScore}, {d.SumMaxScore})
            ON CONFLICT ("UserId", "CharacteristicSerializedName", "ColorType", "CutDirection", "LineIndex", "NoteLineLayer")
            DO UPDATE SET
                "NoteCount" = "PlayNoteAggregates"."NoteCount" + EXCLUDED."NoteCount",
                "GoodCount" = "PlayNoteAggregates"."GoodCount" + EXCLUDED."GoodCount",
                "MissCount" = "PlayNoteAggregates"."MissCount" + EXCLUDED."MissCount",
                "BadCount" = "PlayNoteAggregates"."BadCount" + EXCLUDED."BadCount",
                "SumEarnedScore" = "PlayNoteAggregates"."SumEarnedScore" + EXCLUDED."SumEarnedScore",
                "SumMaxScore" = "PlayNoteAggregates"."SumMaxScore" + EXCLUDED."SumMaxScore"
            """, ct);
    }

    private readonly record struct NoteDelta(
        ColorType ColorType,
        CutDirection CutDirection,
        int LineIndex,
        int NoteLineLayer,
        long NoteCount,
        long GoodCount,
        long MissCount,
        long BadCount,
        long SumEarnedScore,
        long SumMaxScore);
}
