using Microsoft.Extensions.Logging;
using Shiron.BeatDash.API.Services;
using Shiron.BeatDash.Data.Socket;

namespace Shiron.BeatDash.API.Services.Socket.Handlers;

/// <summary>
/// Handles binary <see cref="BinaryPacketTypes.MapCoverImage"/> packets received from the client.
/// </summary>
public sealed class MapCoverImageHandler(
    ILogger<MapCoverImageHandler> logger,
    IMapDataStore mapDataStore,
    IBeatmapPersistenceService persistence
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
            _ = await persistence.PersistAsync(pair, ct);
        }
    }
}
