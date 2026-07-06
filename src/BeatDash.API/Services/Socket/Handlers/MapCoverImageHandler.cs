using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.API.Services;
using Shiron.BeatDash.API.Services.BeatSaver;
using Shiron.BeatDash.Data.Socket;

namespace Shiron.BeatDash.API.Services.Socket.Handlers;

/// <summary>
/// Handles binary <see cref="BinaryPacketTypes.MapCoverImage"/> packets received from the client.
/// </summary>
public sealed class MapCoverImageHandler(
    ILogger<MapCoverImageHandler> logger,
    IMapDataStore mapDataStore,
    IBeatmapPersistenceService persistence,
    IPlaySessionService playSessionService,
    IBeatSaverFetchTrigger beatSaverTrigger,
    IOptions<BeatSaverOptions> beatSaverOptions
) : ISocketBinaryHandler {
    public BinaryPacketTypes PacketType => BinaryPacketTypes.MapCoverImage;

    public async Task HandleAsync(SocketContext context, ReadOnlyMemory<byte> data, CancellationToken ct) {
        if (!MapCoverImagePacket.TryParse(data.ToArray(), out var correlationId, out var png)) {
            logger.LogWarning("Cover image packet too small for a correlation ID ({Bytes} bytes)", data.Length);
            return;
        }

        logger.LogInformation("Received cover image (corr={CorrelationId}, {Bytes} bytes)", correlationId, png.Length);

        var pair = mapDataStore.SubmitImage(context, correlationId, png);
        if (pair is not null) {
            logger.LogInformation("Map data complete: '{SongName}' + {Bytes}-byte image",
                pair.Metadata.SongName, pair.ImageBytes.Length);
            var result = await persistence.PersistAsync(pair, ct);
            logger.LogInformation("Map persisted: mapId={MapId}", result.Id);

            await playSessionService.TryCreateAsync(
                pair.UserId, context.SessionId, correlationId, pair.Metadata, result.Id, ct);

            // Kick off a BeatSaver fetch for maps we've never seen before.
            if (result.IsNew && beatSaverOptions.Value.FetchOnNewMap) {
                await beatSaverTrigger.TriggerMapAsync(result.Id, force: false, ct);
            }
        } else {
            logger.LogWarning(
                "Cover image submitted but pair NOT complete (corr={CorrelationId}) — metadata missing or expired",
                correlationId);
        }
    }
}
