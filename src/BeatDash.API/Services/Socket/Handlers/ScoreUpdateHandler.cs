using Microsoft.Extensions.Logging;
using Shiron.BeatDash.API.Services.Realtime;
using Shiron.BeatDash.Data.Realtime.Events;
using Shiron.BeatDash.Data.Socket;

namespace Shiron.BeatDash.API.Services.Socket.Handlers;

/// <summary>
/// Handles binary <see cref="BinaryPacketTypes.ScoreUpdate"/> packets received from the client
/// on every scoring event. Forwards the snapshot to connected web clients via SignalR.
/// </summary>
public sealed class ScoreUpdateHandler(
    ILogger<ScoreUpdateHandler> logger,
    IRealtimeBroadcaster broadcaster
) : ISocketBinaryHandler {

    /// <inheritdoc/>
    public BinaryPacketTypes PacketType => BinaryPacketTypes.ScoreUpdate;

    public Task HandleAsync(SocketContext context, ReadOnlyMemory<byte> data, CancellationToken ct) {
        if (!ScoreUpdatePacket.TryParse(data.ToArray(), out var packet)) {
            logger.LogWarning("Score update packet too small ({Bytes} bytes)", data.Length);
            return Task.CompletedTask;
        }

        return broadcaster.SendScoreUpdateAsync(context.UserId, new ScoreUpdateEvent(
            packet.CorrelationId,
            packet.SongTime,
            packet.Score,
            packet.MaxScore,
            packet.Accuracy,
            packet.Grade.ToString(),
            packet.Energy,
            packet.Combo,
            packet.Misses,
            DateTime.UtcNow
        ));
    }
}
