using Microsoft.Extensions.Caching.Memory;
using Shiron.BeatDash.Data.Socket;

namespace Shiron.BeatDash.API.Services.Socket;

/// <summary>
/// Temporarily holds the two halves of a map-start event (JSON metadata and
/// cover image) keyed by a server-assigned correlation ID, joining them into a
/// complete <see cref="MapDataPair"/> once both arrive. Unmatched halves expire
/// automatically, so an orphan from a dropped send never leaks.
/// </summary>
public interface IMapDataStore {
    /// <summary>
    /// Records the metadata half under the server-assigned
    /// <paramref name="correlationId"/>. Returns a complete pair if the image
    /// already arrived.
    /// </summary>
    MapDataPair? SubmitMetadata(SocketContext ctx, int correlationId, MapStartMessage metadata);

    /// <summary>
    /// Records the image half. Returns a complete pair if the metadata already arrived.
    /// </summary>
    MapDataPair? SubmitImage(SocketContext ctx, int correlationId, byte[] image);
}

/// <summary>
/// A fully joined map-start event: metadata paired with its cover image, plus
/// the connection context needed to persist or forward it.
/// </summary>
public sealed record MapDataPair(
    MapStartMessage Metadata,
    byte[] ImageBytes,
    Guid UserId,
    Guid ClientId
);

/// <summary>
/// <see cref="IMemoryCache"/>-backed implementation. Entries are scoped per
/// session and correlation ID and expire shortly after creation.
/// </summary>
public sealed class MapDataStore(IMemoryCache cache) : IMapDataStore {
    /// <summary>
    /// How long an unmatched half lives before eviction. The metadata and image
    /// are sent back-to-back, so this only needs to cover transmission time.
    /// </summary>
    private static readonly TimeSpan Expiration = TimeSpan.FromMinutes(1);

    /// <inheritdoc/>
    public MapDataPair? SubmitMetadata(SocketContext ctx, int correlationId, MapStartMessage metadata) {
        var pending = GetOrCreate(ctx.SessionId, correlationId);
        pending.Metadata = metadata;
        return TryComplete(ctx, correlationId, pending);
    }

    /// <inheritdoc/>
    public MapDataPair? SubmitImage(SocketContext ctx, int correlationId, byte[] image) {
        var pending = GetOrCreate(ctx.SessionId, correlationId);
        pending.Image = image;
        return TryComplete(ctx, correlationId, pending);
    }

    private PendingMapData GetOrCreate(Guid sessionId, int correlationId) =>
        cache.GetOrCreate(Key(sessionId, correlationId), entry => {
            entry.SetAbsoluteExpiration(Expiration);
            return new PendingMapData();
        })!;

    private MapDataPair? TryComplete(SocketContext ctx, int correlationId, PendingMapData pending) {
        if (!pending.IsComplete) return null;

        cache.Remove(Key(ctx.SessionId, correlationId));
        return new MapDataPair(pending.Metadata!, pending.Image!, ctx.UserId, ctx.ClientId);
    }

    private static string Key(Guid sessionId, int correlationId) =>
        $"mapdata:{sessionId:N}:{correlationId}";

    private sealed class PendingMapData {
        public MapStartMessage? Metadata;
        public byte[]? Image;

        public bool IsComplete => Metadata is not null && Image is not null;
    }
}
