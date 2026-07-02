using Microsoft.Extensions.Logging;
using Shiron.BeatDash.API.Services;
using Shiron.BeatDash.Data.Socket;

namespace Shiron.BeatDash.API.Services.Socket.Handlers;

/// <summary>
/// Handles <see cref="MapStartMessage"/> received from the client when a beatmap starts.
/// </summary>
public sealed class MapStartHandler(
    ILogger<MapStartHandler> logger,
    IMapDataStore mapDataStore,
    IBeatmapPersistenceService persistence
) : SocketMessageHandler<MapStartMessage> {

    protected override async Task HandleMessageAsync(
        SocketContext context, MapStartMessage message, CancellationToken ct) {
        logger.LogInformation("Map started: {SongName} (corr={CorrelationId})", message.SongName, message.CorrelationId);

        var pair = mapDataStore.SubmitMetadata(context, message);
        if (pair is not null) {
            logger.LogInformation("Map data complete: '{SongName}' + {Bytes}-byte image",
                pair.Metadata.SongName, pair.ImageBytes.Length);
            await persistence.PersistAsync(pair, ct);
        }
    }
}
