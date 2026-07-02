using System.Text.Json;
using Shiron.BeatDash.Data.Socket;

namespace Shiron.BeatDash.API.Services.Socket;

/// <summary>
/// Defines a handler for incoming socket text (JSON) messages.
/// Implementations are keyed by message type name and resolved via DI.
/// Prefer deriving from <see cref="SocketMessageHandler{T}"/> for type-safe deserialization.
/// </summary>
public interface ISocketMessageHandler {
    /// <summary>
    /// Handles a raw socket text message payload (UTF-8 JSON bytes).
    /// </summary>
    Task HandleAsync(SocketContext context, ReadOnlyMemory<byte> json, CancellationToken ct);
}

/// <summary>
/// Base class for type-safe handlers of a specific <see cref="SocketMessage{T}"/> type.
/// Automatically deserializes the JSON payload before invoking the handler logic.
/// </summary>
/// <typeparam name="T">The socket message type to handle.</typeparam>
/// <remarks>
/// Register with <c>services.AddSocketMessageHandler&lt;TMessage, THandler&gt;()</c>.
/// </remarks>
public abstract class SocketMessageHandler<T> : ISocketMessageHandler where T : SocketMessage<T> {
    private static readonly JsonSerializerOptions SerializerOptions = new() {
        PropertyNameCaseInsensitive = true
    };

    async Task ISocketMessageHandler.HandleAsync(SocketContext context, ReadOnlyMemory<byte> json, CancellationToken ct) {
        var message = JsonSerializer.Deserialize<T>(json.Span, SerializerOptions);
        if (message is null) return;
        await HandleMessageAsync(context, message, ct);
    }

    /// <summary>
    /// Handles the deserialized socket message.
    /// </summary>
    /// <param name="context">The active connection context.</param>
    /// <param name="message">The deserialized message.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    protected abstract Task HandleMessageAsync(SocketContext context, T message, CancellationToken ct);
}
