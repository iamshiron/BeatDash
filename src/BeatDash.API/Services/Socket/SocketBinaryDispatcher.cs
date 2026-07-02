using Shiron.BeatDash.Data.Socket;
using Microsoft.Extensions.Logging;

namespace Shiron.BeatDash.API.Services.Socket;

/// <summary>
/// Routes incoming socket binary packets to registered handlers
/// based on the packet's type byte (<see cref="BinaryPacketTypes"/>).
/// Falls back to a logged warning when no handler matches.
/// </summary>
public sealed class SocketBinaryDispatcher(
    IServiceProvider services,
    ILogger<SocketBinaryDispatcher> logger
) {
    /// <summary>
    /// Size of the binary packet header: 4-byte length prefix + 1-byte type.
    /// </summary>
    private const int HeaderSize = 5;

    /// <summary>
    /// Parses a binary packet, resolves the matching handler, and dispatches.
    /// Logs a warning if no handler is registered for the packet type.
    /// </summary>
    public async Task DispatchAsync(SocketContext context, ReadOnlyMemory<byte> payload, CancellationToken ct) {
        if (payload.Length < HeaderSize) {
            logger.LogWarning("Received binary packet smaller than header ({Size} bytes)", payload.Length);
            return;
        }

        var typeByte = payload.Span[4];
        if (!Enum.IsDefined(typeof(BinaryPacketTypes), typeByte)) {
            logger.LogWarning("Received binary packet with unknown type byte 0x{PacketType:X2}", typeByte);
            return;
        }

        var packetType = (BinaryPacketTypes) typeByte;
        var handler = services.GetKeyedService<ISocketBinaryHandler>(packetType);
        if (handler is null) {
            logger.LogWarning("No handler registered for binary packet type '{PacketType}'", packetType);
            return;
        }

        var data = payload[HeaderSize..];
        try {
            await handler.HandleAsync(context, data, ct);
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            logger.LogError(ex, "Unhandled error in handler for binary packet type '{PacketType}'", packetType);
        }
    }
}
