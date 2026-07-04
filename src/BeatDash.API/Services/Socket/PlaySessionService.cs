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
    /// Sets <see cref="PlaySession.EndedAt"/> on the session matching the given
    /// correlation ID, if one exists and hasn't already been ended.
    /// </summary>
    Task TryEndAsync(Guid socketSessionId, int correlationId, CancellationToken ct);
}

/// <summary>
/// <see cref="IDbContextFactory{TContext}"/>-backed implementation.
/// </summary>
public sealed class PlaySessionService(
    IDbContextFactory<BeatDashDbContext> dbFactory,
    IPlaySessionStore sessionStore,
    ILogger<PlaySessionService> logger
) : IPlaySessionService {

    /// <inheritdoc/>
    public async Task<Guid?> TryCreateAsync(
        Guid userId, Guid socketSessionId, int correlationId,
        MapStartMessage metadata, Guid beatmapId, CancellationToken ct) {

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
    public async Task TryEndAsync(Guid socketSessionId, int correlationId, CancellationToken ct) {
        if (!sessionStore.TryGet(socketSessionId, correlationId, out var sessionId)) {
            logger.LogWarning(
                "Cannot end play session: no session registered (corr={CorrelationId})",
                correlationId);
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var updated = await db.PlaySessions
            .Where(s => s.Id == sessionId && s.EndedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.EndedAt, DateTime.UtcNow), ct);

        if (updated > 0) {
            logger.LogInformation(
                "Ended play session {SessionId} (corr={CorrelationId})",
                sessionId, correlationId);
        }
    }
}
