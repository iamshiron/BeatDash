using Microsoft.Extensions.Logging;
using Shiron.BeatDash.API.Services.Realtime;
using Shiron.BeatDash.Data.Realtime.Events;
using Shiron.BeatDash.Data.Socket;

namespace Shiron.BeatDash.API.Services.Socket.Handlers;

/// <summary>
/// Handles <see cref="LiveStatsMessage"/> received over the reliable TCP channel.
/// The batched event arrays (note events, combo breaks, energy changes, score
/// changes) and the cumulative snapshot form the authoritative integrity/statistics
/// record destined for database persistence (persistence TBD).
/// </summary>
public sealed class LiveStatsHandler(
    ILogger<LiveStatsHandler> logger,
    IRealtimeBroadcaster broadcaster
) : SocketMessageHandler<LiveStatsMessage> {
    protected async override Task HandleMessageAsync(
        SocketContext context,
        LiveStatsMessage message,
        CancellationToken ct) {
        var noteEvents = message.NoteEvents ?? [];
        var comboBreaks = message.ComboBreaks ?? [];
        var energyChanges = message.EnergyChanges ?? [];
        var scoreChanges = message.ScoreChanges ?? [];

        logger.LogInformation(
            "Live stats received (user={UserId}, corr={CorrelationId}): songTimeMs={SongTime}, " +
            "score={Score}/{MaxPossibleScore}, combo={CurrentCombo}, energy={Energy:F2} | " +
            "items: notes={Notes}, comboBreaks={ComboBreaks}, energyChanges={EnergyChanges}, " +
            "scoreChanges={ScoreChanges}, total={Total}",
            context.UserId, message.CorrelationId, message.SongTime,
            message.Score, message.MaxPossibleScore, message.CurrentCombo, message.Energy,
            noteEvents.Length, comboBreaks.Length, energyChanges.Length, scoreChanges.Length,
            noteEvents.Length + comboBreaks.Length + energyChanges.Length + scoreChanges.Length);

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
            noteEvents,
            comboBreaks,
            energyChanges,
            scoreChanges,
            DateTime.UtcNow
        ));
    }
}
