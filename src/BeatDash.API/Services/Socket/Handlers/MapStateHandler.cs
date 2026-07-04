using Microsoft.Extensions.Logging;
using Shiron.BeatDash.API.Services.Realtime;
using Shiron.BeatDash.Data.Realtime.Events;
using Shiron.BeatDash.Data.Socket;

namespace Shiron.BeatDash.API.Services.Socket.Handlers;

/// <summary>
/// Handles <see cref="MapStateMessage"/> received from the client when a beatmap's
/// gameplay state changes (paused, resumed, finished, failed, or quit).
/// </summary>
public sealed class MapStateHandler(
    ILogger<MapStateHandler> logger,
    IRealtimeBroadcaster broadcaster,
    IPlaySessionService playSessionService,
    IPlaySessionStore sessionStore,
    IMotionFramePersistence motionFramePersistence
) : SocketBinaryMessageHandler<MapStateMessage> {

    /// <inheritdoc/>
    public override BinaryPacketTypes PacketType => BinaryPacketTypes.MapState;

    protected override async Task HandleMessageAsync(
        SocketContext context, MapStateMessage message, CancellationToken ct) {
        logger.LogInformation("Map state changed: {State} (corr={CorrelationId}, level={LevelId})",
            message.State, message.CorrelationId, message.LevelId);

        if (IsTerminalState(message.State)) {
            await playSessionService.TryEndAsync(context.SessionId, message.CorrelationId, ct);

            if (sessionStore.TryGet(context.SessionId, message.CorrelationId, out var playSessionId)) {
                _ = motionFramePersistence.PersistAsync(
                    context.SessionId, message.CorrelationId, playSessionId, CancellationToken.None);
            } else {
                logger.LogWarning(
                    "Cannot flush motion frames: no play session registered (corr={CorrelationId})",
                    message.CorrelationId);
            }
        }

        await broadcaster.SendLiveMapStateChangedAsync(context.UserId, new LiveMapStateChangedEvent(
            null,
            message.CorrelationId,
            message.State,
            message.Results,
            DateTime.UtcNow
        ));
    }

    /// <summary>
    /// Terminal states that end the play session (carry <see cref="MapResults"/>).
    /// </summary>
    private static bool IsTerminalState(string state) =>
        state is "Finished" or "Failed" or "Quit";
}
