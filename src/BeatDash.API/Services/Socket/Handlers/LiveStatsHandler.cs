using Microsoft.Extensions.Logging;
using Shiron.BeatDash.API.Services.Realtime;
using Shiron.BeatDash.Data.Realtime.Events;
using Shiron.BeatDash.Data.Socket;

namespace Shiron.BeatDash.API.Services.Socket.Handlers;

public sealed class LiveStatsHandler(
    ILogger<LiveStatsHandler> logger,
    IRealtimeBroadcaster broadcaster
) : SocketMessageHandler<LiveStatsMessage> {

    protected override async Task HandleMessageAsync(
        SocketContext context, LiveStatsMessage message, CancellationToken ct) {
        logger.LogDebug("Live stats: score={Score}, combo={Combo}, songTime={SongTime}s (corr={CorrelationId})",
            message.Score, message.CurrentCombo, message.SongTime, message.CorrelationId);

        await broadcaster.SendLiveStatsAsync(context.UserId, new LiveStatsEvent(
            message.CorrelationId,
            message.SongTime,
            message.Score,
            message.ModifiedScore,
            message.MaxPossibleScore,
            message.Energy,
            message.CurrentCombo,
            message.MaxCombo,
            message.LeftHand,
            message.RightHand,
            message.NoteEvents ?? [],
            message.ComboBreaks ?? [],
            message.EnergyChanges ?? [],
            DateTime.UtcNow
        ));
    }
}
