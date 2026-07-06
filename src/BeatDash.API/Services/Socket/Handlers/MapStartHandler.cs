using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.API.Services;
using Shiron.BeatDash.API.Services.BeatSaver;
using Shiron.BeatDash.API.Services.Realtime;
using Shiron.BeatDash.Data.Realtime.Events;
using Shiron.BeatDash.Data.Socket;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.API.Services.Socket.Handlers;

/// <summary>
/// Handles <see cref="MapStartMessage"/> received from the client when a beatmap starts.
/// </summary>
public sealed class MapStartHandler(
    ILogger<MapStartHandler> logger,
    IMapDataStore mapDataStore,
    IBeatmapPersistenceService persistence,
    IRealtimeBroadcaster broadcaster,
    IDbContextFactory<BeatDashDbContext> dbFactory,
    IPlaySessionService playSessionService,
    IBeatSaverFetchTrigger beatSaverTrigger,
    IOptions<BeatSaverOptions> beatSaverOptions
) : SocketBinaryMessageHandler<MapStartMessage> {

    /// <inheritdoc/>
    public override BinaryPacketTypes PacketType => BinaryPacketTypes.MapStart;

    protected override async Task HandleMessageAsync(
        SocketContext context, MapStartMessage message, CancellationToken ct) {
        var correlationId = Random.Shared.Next(1, int.MaxValue);

        logger.LogInformation(
            "Map started: {SongName} (assigned corr={CorrelationId}, level={LevelId}) | " +
            "context items: npsCurve={Nps}, walls={Walls}, bombs={Bombs}, notesPerHand={NotesLeft}/{NotesRight}",
            message.SongName, correlationId, message.LevelId,
            (message.NpsCurve ?? []).Length, (message.WallTimeline ?? []).Length, (message.BombPositions ?? []).Length,
            message.NotesPerHandLeft, message.NotesPerHandRight);

        await context.SessionManager.SendMessageAsync(
            context.SessionId,
            new CorrelationAssignedMessage { CorrelationId = correlationId },
            ct);

        var pair = mapDataStore.SubmitMetadata(context, correlationId, message);
        Guid? mapId = null;
        if (pair is not null) {
            logger.LogInformation("Map data complete: '{SongName}' + {Bytes}-byte image",
                pair.Metadata.SongName, pair.ImageBytes.Length);
            var result = await persistence.PersistAsync(pair, ct);
            mapId = result.Id;
            await playSessionService.TryCreateAsync(
                context.UserId, context.SessionId, correlationId, message, mapId.Value, ct);

            // Kick off a BeatSaver fetch for maps we've never seen before.
            if (result.IsNew && beatSaverOptions.Value.FetchOnNewMap) {
                await beatSaverTrigger.TriggerMapAsync(result.Id, force: false, ct);
            }
        } else {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            mapId = await db.Beatmaps
                .AsNoTracking()
                .Where(b => b.LevelId == message.LevelId)
                .Select(b => (Guid?) b.Id)
                .FirstOrDefaultAsync(ct);

            if (mapId is Guid existingMapId) {
                await playSessionService.TryCreateAsync(
                    context.UserId, context.SessionId, correlationId, message, existingMapId, ct);
            } else {
                logger.LogWarning(
                    "Beatmap not yet persisted; play session will be created when cover image arrives (corr={CorrelationId})",
                    correlationId);
            }
        }

        await broadcaster.SendLiveMapStartedAsync(context.UserId, new LiveMapStartedEvent(
            mapId,
            message.SongName,
            message.SongSubName,
            message.SongAuthor,
            message.Mapper,
            message.Bpm,
            message.DurationMs,
            message.Difficulty,
            message.DifficultyName,
            message.NotesPerSecond,
            message.NoteJumpSpeed,
            message.BombCount,
            message.ObstacleCount,
            message.CuttableObjectCount,
            message.LaneCount,
            message.Characteristic.SerializedName,
            message.ModifierFlags,
            message.SongSpeed,
            message.NotesPerHandLeft,
            message.NotesPerHandRight,
            message.NpsCurve ?? [],
            message.WallTimeline ?? [],
            message.BombPositions ?? [],
            message.AutoMode,
            DateTime.UtcNow
        ));
    }
}
