using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shiron.BeatDash.API.Services.Realtime;
using Shiron.BeatDash.Data.Realtime.Events;
using Shiron.BeatDash.Data.Socket;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.API.Services.Socket.Handlers;

/// <summary>
/// Handles <see cref="LiveStatsMessage"/> received over the reliable TCP channel.
/// The batched event arrays (note events, combo breaks, energy changes, score
/// changes) are persisted as <see cref="PlaySession"/> item rows, keyed by the
/// play session resolved from the correlation ID. The cumulative snapshot is
/// forwarded to realtime subscribers.
/// </summary>
public sealed class LiveStatsHandler(
    ILogger<LiveStatsHandler> logger,
    IRealtimeBroadcaster broadcaster,
    IDbContextFactory<BeatDashDbContext> dbFactory,
    IPlaySessionStore sessionStore
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

        if (!sessionStore.TryGet(context.SessionId, message.CorrelationId, out var playSessionId)) {
            logger.LogWarning(
                "No play session registered for corr={CorrelationId} (user={UserId}); skipping persistence",
                message.CorrelationId, context.UserId);
            return;
        }

        await PersistItemsAsync(
            playSessionId, message.CorrelationId,
            noteEvents, comboBreaks, energyChanges, scoreChanges, ct);
    }

    private async Task PersistItemsAsync(
        Guid playSessionId, int correlationId,
        NoteEventDto[] noteEvents, ComboBreakDto[] comboBreaks,
        EnergyChangeDto[] energyChanges, ScoreChangeDto[] scoreChanges,
        CancellationToken ct) {
        if (noteEvents.Length == 0 && comboBreaks.Length == 0
            && energyChanges.Length == 0 && scoreChanges.Length == 0) {
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        if (noteEvents.Length > 0) {
            db.PlaySessionNoteItems.AddRange(noteEvents.Select(e => new PlaySessionNoteItem {
                PlaySessionId = playSessionId,
                CorrelationId = correlationId,
                SongTimeMs = e.SongTime,
                ColorType = (ColorType) e.ColorType,
                NoteType = (NoteType) e.NoteType,
                ScoringType = (NoteScoringType) e.ScoringType,
                CutDirection = (CutDirection) e.CutDirection,
                LineIndex = e.LineIndex,
                NoteLineLayer = e.NoteLineLayer,
                Result = e.Result,
                MaxScore = e.MaxScore,
                BeforeCutScore = e.BeforeCutScore,
                CenterDistanceScore = e.CenterDistanceScore,
                AfterCutScore = e.AfterCutScore,
                PreCutSwing = e.BeforeCutSwing,
                PostCutSwing = e.AfterCutSwing,
                CutPointDistance = e.CutPointDistance,
                SaberSpeed = e.SaberSpeed,
            }));
        }

        if (comboBreaks.Length > 0) {
            db.PlaySessionComboBreakItems.AddRange(comboBreaks.Select(c => new PlaySessionComboBreakItem {
                PlaySessionId = playSessionId,
                CorrelationId = correlationId,
                SongTimeMs = c.SongTime,
                ComboBefore = c.ComboBefore,
            }));
        }

        if (energyChanges.Length > 0) {
            db.PlaySessionEnergyChangeItems.AddRange(energyChanges.Select(e => new PlaySessionEnergyChangeItem {
                PlaySessionId = playSessionId,
                CorrelationId = correlationId,
                SongTimeMs = e.SongTime,
                Energy = e.Energy,
            }));
        }

        if (scoreChanges.Length > 0) {
            db.PlaySessionScoreChangeItems.AddRange(scoreChanges.Select(s => new PlaySessionScoreChangeItem {
                PlaySessionId = playSessionId,
                CorrelationId = correlationId,
                SongTimeMs = s.SongTime,
                Score = s.Score,
            }));
        }

        await db.SaveChangesAsync(ct);
    }
}
