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
    IRealtimeBroadcaster broadcaster
) : SocketMessageHandler<MapStateMessage> {

    protected override async Task HandleMessageAsync(
        SocketContext context, MapStateMessage message, CancellationToken ct) {
        logger.LogInformation("Map state changed: {State} (corr={CorrelationId}, level={LevelId})",
            message.State, message.CorrelationId, message.LevelId);

        await broadcaster.SendLiveMapStateChangedAsync(context.UserId, new LiveMapStateChangedEvent(
            null,
            message.CorrelationId,
            message.State,
            message.Results,
            DateTime.UtcNow
        ));
    }
}
