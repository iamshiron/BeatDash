using Microsoft.Extensions.Caching.Memory;

namespace Shiron.BeatDash.API.Services.Socket;

/// <summary>
/// Maps a server-assigned correlation ID to the persisted <c>PlaySession</c> ID
/// for the duration of a play. Populated by <c>MapStartHandler</c> when a
/// session is created and read by <c>LiveStatsHandler</c> to append item rows.
/// Entries expire automatically so an abandoned play never leaks.
/// </summary>
public interface IPlaySessionStore {
    /// <summary>Records the <c>PlaySession</c> ID for the given correlation ID.</summary>
    void Register(Guid sessionId, int correlationId, Guid playSessionId);

    /// <summary>Resolves the <c>PlaySession</c> ID, if still registered.</summary>
    bool TryGet(Guid sessionId, int correlationId, out Guid playSessionId);
}

/// <summary>
/// <see cref="IMemoryCache"/>-backed implementation. Entries are scoped per
/// socket session and correlation ID and expire after a period long enough to
/// cover a full play (including pauses).
/// </summary>
public sealed class PlaySessionStore(IMemoryCache cache) : IPlaySessionStore {
    private static readonly TimeSpan Expiration = TimeSpan.FromMinutes(30);

    /// <inheritdoc/>
    public void Register(Guid sessionId, int correlationId, Guid playSessionId) {
        cache.Set(Key(sessionId, correlationId), playSessionId, Expiration);
    }

    /// <inheritdoc/>
    public bool TryGet(Guid sessionId, int correlationId, out Guid playSessionId) {
        return cache.TryGetValue(Key(sessionId, correlationId), out playSessionId);
    }

    private static string Key(Guid sessionId, int correlationId) =>
        $"playsession:{sessionId:N}:{correlationId}";
}
