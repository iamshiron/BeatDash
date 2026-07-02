using Microsoft.Extensions.Logging;
using Shiron.BeatDash.Data.Socket;

namespace Shiron.BeatDash.API.Services.Socket.Handlers;

/// <summary>
/// Handles binary <see cref="BinaryPacketTypes.MapCoverImage"/> packets received from the client.
/// </summary>
public sealed class MapCoverImageHandler(ILogger<MapCoverImageHandler> logger) : ISocketBinaryHandler {
    public BinaryPacketTypes PacketType => BinaryPacketTypes.MapCoverImage;

    public Task HandleAsync(SocketContext context, ReadOnlyMemory<byte> data, CancellationToken ct) {
        logger.LogInformation("Received cover image, length: {Length} bytes", data.Length);
        return Task.CompletedTask;
    }
}
