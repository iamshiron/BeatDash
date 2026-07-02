using Shiron.BeatDash.Data.Socket;

namespace Shiron.BeatDash.API.Services.Socket;

/// <summary>
/// Defines a handler for incoming socket binary packets.
/// Implementations are keyed by <see cref="BinaryPacketTypes"/> and resolved via DI.
/// </summary>
public interface ISocketBinaryHandler {
    /// <summary>
    /// The binary packet type this handler processes.
    /// </summary>
    BinaryPacketTypes PacketType { get; }

    /// <summary>
    /// Handles the binary packet payload (excluding the 5-byte packet header).
    /// </summary>
    /// <param name="context">The active connection context.</param>
    /// <param name="data">The packet data (after the header).</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    Task HandleAsync(SocketContext context, ReadOnlyMemory<byte> data, CancellationToken ct);
}
