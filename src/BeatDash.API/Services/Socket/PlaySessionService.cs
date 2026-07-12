using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shiron.BeatDash.Data.Socket;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.API.Services.Socket;

/// <summary>
/// Creates <see cref="PlaySession"/> rows once a beatmap is guaranteed to be
/// persisted. Called from whichever handler completes the map-data pair
/// (metadata + cover image), eliminating the timing gap where a new map's
/// session was created before the beatmap existed in the database.
/// </summary>
public interface IPlaySessionService {
    /// <summary>
    /// Resolves the played <see cref="BeatmapDifficulty"/> and creates a
    /// <see cref="PlaySession"/> row, registering the correlation ID so
    /// <see cref="LiveStatsHandler"/> can append item rows.
    /// </summary>
    /// <returns>The created session ID, or <c>null</c> if it could not be created.</returns>
    Task<Guid?> TryCreateAsync(
        Guid userId, Guid socketSessionId, int correlationId,
        MapStartMessage metadata, Guid beatmapId, CancellationToken ct);

    /// <summary>
    /// Sets <see cref="PlaySession.EndedAt"/> and <see cref="PlaySession.EndReason"/>
    /// on the session matching the given correlation ID, if one exists and hasn't
    /// already been ended. When <paramref name="results"/> is non-null, also
    /// persists the final results; otherwise records
    /// <see cref="PlaySessionEndReason.Incomplete"/>.
    /// </summary>
    Task TryEndAsync(Guid socketSessionId, int correlationId, string state, MapResults? results, CancellationToken ct);
}

/// <summary>
/// <see cref="IDbContextFactory{TContext}"/>-backed implementation.
/// </summary>
public sealed class PlaySessionService(
    IDbContextFactory<BeatDashDbContext> dbFactory,
    IPlaySessionStore sessionStore,
    IWeaknessAggregationService weaknessAggregation,
    ILogger<PlaySessionService> logger
) : IPlaySessionService {

    /// <inheritdoc/>
    public async Task<Guid?> TryCreateAsync(
        Guid userId, Guid socketSessionId, int correlationId,
        MapStartMessage metadata, Guid beatmapId, CancellationToken ct) {

        if (sessionStore.TryGet(socketSessionId, correlationId, out var existingSessionId)) {
            logger.LogInformation(
                "Play session already registered (corr={CorrelationId}, session={SessionId}); skipping duplicate",
                correlationId, existingSessionId);
            return existingSessionId;
        }

        if (!Enum.TryParse(metadata.Difficulty, ignoreCase: true, out BeatmapDifficultyRank rank)) {
            logger.LogWarning(
                "Skipping play session creation: unrecognized difficulty '{Difficulty}'",
                metadata.Difficulty);
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var difficultyId = await db.BeatmapDifficulties
            .AsNoTracking()
            .Where(d => d.BeatmapId == beatmapId
                && d.CharacteristicSerializedName == metadata.Characteristic.SerializedName
                && d.DifficultyRank == rank)
            .Select(d => (Guid?) d.Id)
            .FirstOrDefaultAsync(ct);

        if (difficultyId is not Guid beatmapDifficultyId) {
            logger.LogWarning(
                "Skipping play session creation: difficulty not found (map={MapId}, char={Char}, rank={Rank})",
                beatmapId, metadata.Characteristic.SerializedName, rank);
            return null;
        }

        var session = new PlaySession {
            CorrelationId = correlationId,
            UserId = userId,
            BeatmapDifficultyId = beatmapDifficultyId,
            StartedAt = DateTime.UtcNow,
            EndedAt = null,
            AutoMode = metadata.AutoMode,
            ModifierFlags = metadata.ModifierFlags,
        };
        db.PlaySessions.Add(session);
        await db.SaveChangesAsync(ct);

        sessionStore.Register(socketSessionId, correlationId, session.Id);
        logger.LogInformation(
            "Created play session {SessionId} (corr={CorrelationId})",
            session.Id, correlationId);
        return session.Id;
    }

    /// <inheritdoc/>
    public async Task TryEndAsync(Guid socketSessionId, int correlationId, string state, MapResults? results, CancellationToken ct) {
        if (!sessionStore.TryGet(socketSessionId, correlationId, out var sessionId)) {
            logger.LogWarning(
                "Cannot end play session: no session registered (corr={CorrelationId})",
                correlationId);
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        if (results is null) {
            var updated = await db.PlaySessions
                .Where(s => s.Id == sessionId && s.EndedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.EndedAt, DateTime.UtcNow)
                    .SetProperty(x => x.EndReason, (PlaySessionEndReason?) PlaySessionEndReason.Incomplete), ct);

            if (updated > 0) {
                logger.LogInformation(
                    "Ended play session {SessionId} (corr={CorrelationId}, reason=Incomplete)",
                    sessionId, correlationId);
            }
            return;
        }

        var session = await db.PlaySessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null) {
            logger.LogWarning(
                "Play session {SessionId} not found in database (corr={CorrelationId})",
                sessionId, correlationId);
            return;
        }

        session.EndedAt ??= DateTime.UtcNow;
        session.EndReason ??= ToEndReason(state);
        session.Results = new PlaySessionResults {
            Score = results.Score,
            MultipliedScore = results.MultipliedScore,
            MaxPossibleScore = results.MaxMultipliedScore,
            Accuracy = results.Accuracy,
            Rank = results.Rank,
            FullCombo = results.FullCombo,
            MaxCombo = results.MaxCombo,
            GoodCuts = results.GoodCuts,
            BadCuts = results.BadCuts,
            Misses = results.MissedNotes,
            FinalEnergy = results.Energy,
            EndSongTimeMs = (int) (results.EndSongTime * 1000f),
        };

        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Ended play session {SessionId} with results (corr={CorrelationId}, reason={EndReason}, rank={Rank}, score={Score})",
            sessionId, correlationId, session.EndReason, results.Rank, results.Score);

        // Fold this play's notes into the lifetime weakness aggregate. Cheap (a few
        // hundred rows), but guarded so an aggregation failure never fails finalization.
        if (session is { EndReason: PlaySessionEndReason.Finished, AutoMode: false }) {
            try {
                await weaknessAggregation.FoldSessionAsync(sessionId, ct);
            } catch (Exception ex) when (ex is not OperationCanceledException) {
                logger.LogError(ex,
                    "Failed to fold weakness aggregate for play session {SessionId} (corr={CorrelationId})",
                    sessionId, correlationId);
            }
        }
    }

    /// <summary>
    /// Maps the client-reported state string to a persisted end reason. Mirrors
    /// the terminal set checked by <c>MapStateHandler.IsTerminalState</c>.
    /// </summary>
    private static PlaySessionEndReason ToEndReason(string state) => state switch {
        "Finished" => PlaySessionEndReason.Finished,
        "Failed" => PlaySessionEndReason.Failed,
        "Quit" => PlaySessionEndReason.Quit,
        _ => PlaySessionEndReason.Incomplete
    };
}
