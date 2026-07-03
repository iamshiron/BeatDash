using Shiron.BeatDash.API.Services.Realtime;
using Shiron.BeatDash.Data.Realtime.Events;
using Shiron.BeatDash.Data.Socket;

namespace Shiron.BeatDash.API.Services.Socket.Handlers;

/// <summary>
/// Handles <see cref="ScoreUpdateMessage"/> received from the client on every scoring event.
/// Forwards the snapshot to connected web clients via SignalR for dashboard animations.
/// </summary>
public sealed class ScoreUpdateHandler(
    IRealtimeBroadcaster broadcaster
) : SocketMessageHandler<ScoreUpdateMessage> {

    protected override Task HandleMessageAsync(
        SocketContext context, ScoreUpdateMessage message, CancellationToken ct) {

        return broadcaster.SendScoreUpdateAsync(context.UserId, new ScoreUpdateEvent(
            message.CorrelationId,
            message.SongTime,
            message.Score,
            message.MaxScore,
            message.Accuracy,
            message.Rank,
            message.Energy,
            message.Combo,
            DateTime.UtcNow
        ));
    }
}
