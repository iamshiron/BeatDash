using System.Text.Json;
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

/// <summary>
/// Base class for binary packets whose payload is a UTF-8 JSON message.
/// Automatically deserializes the payload before invoking the handler logic.
/// </summary>
/// <typeparam name="T">The message type to deserialize.</typeparam>
/// <remarks>
/// Register with <c>services.AddSocketBinaryHandler&lt;THandler&gt;(packetType)</c>.
/// </remarks>
public abstract class SocketBinaryMessageHandler<T> : ISocketBinaryHandler where T : class {
    private static readonly JsonSerializerOptions SerializerOptions = new() {
        PropertyNameCaseInsensitive = true
    };

    /// <inheritdoc/>
    public abstract BinaryPacketTypes PacketType { get; }

    async Task ISocketBinaryHandler.HandleAsync(SocketContext context, ReadOnlyMemory<byte> data, CancellationToken ct) {
        var message = JsonSerializer.Deserialize<T>(data.Span, SerializerOptions);
        if (message is null) return;
        await HandleMessageAsync(context, message, ct);
    }

    /// <summary>
    /// Handles the deserialized message.
    /// </summary>
    /// <param name="context">The active connection context.</param>
    /// <param name="message">The deserialized message.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    protected abstract Task HandleMessageAsync(SocketContext context, T message, CancellationToken ct);
}
