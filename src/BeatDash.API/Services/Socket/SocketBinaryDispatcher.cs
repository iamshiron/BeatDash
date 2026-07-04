using Shiron.BeatDash.Data.Socket;
using Microsoft.Extensions.Logging;

namespace Shiron.BeatDash.API.Services.Socket;

/// <summary>
/// Central, transport- and envelope-agnostic router for incoming binary packets.
/// Both the TCP WebSocket and the UDP socket strip their own envelope, then feed
/// this dispatcher the packet type and its payload. Handlers are therefore shared
/// across both transports and never deal with framing.
/// </summary>
public sealed class SocketBinaryDispatcher(
    IServiceProvider services,
    ILogger<SocketBinaryDispatcher> logger
) {
    /// <summary>
    /// Resolves and invokes the handler registered for <paramref name="type"/>,
    /// passing only the packet payload (no envelope). This is the envelope-agnostic
    /// entry point shared by both transports.
    /// </summary>
    public async Task DispatchAsync(SocketContext context, BinaryPacketTypes type, ReadOnlyMemory<byte> content, CancellationToken ct) {
        var handler = services.GetKeyedService<ISocketBinaryHandler>(type);
        if (handler is null) {
            logger.LogWarning("No handler registered for binary packet type '{PacketType}'", type);
            return;
        }

        try {
            await handler.HandleAsync(context, content, ct);
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            logger.LogError(ex, "Unhandled error in handler for binary packet type '{PacketType}'", type);
        }
    }

    /// <summary>
    /// TCP convenience overload: strips the <see cref="BinaryPacket"/> envelope from
    /// a raw WebSocket message, then routes through the envelope-agnostic core above.
    /// </summary>
    public async Task DispatchAsync(SocketContext context, ReadOnlyMemory<byte> packet, CancellationToken ct) {
        if (packet.Length < BinaryPacket.HeaderSize) {
            logger.LogWarning("Received binary packet smaller than header ({Size} bytes)", packet.Length);
            return;
        }

        var typeByte = packet.Span[BinaryPacket.HeaderSize - 1];
        if (!Enum.IsDefined(typeof(BinaryPacketTypes), typeByte)) {
            logger.LogWarning("Received binary packet with unknown type byte 0x{PacketType:X2}", typeByte);
            return;
        }

        var type = (BinaryPacketTypes) typeByte;
        var payloadSize = packet.Length - BinaryPacket.HeaderSize;
        logger.LogInformation(
            "TCP binary dispatch: type={PacketType}, payload={Size} bytes, session={SessionId}",
            type, payloadSize, context.SessionId);

        await DispatchAsync(context, type, packet[BinaryPacket.HeaderSize..], ct);
    }
}
