using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Shiron.BeatDash.API.Services.Socket;

/// <summary>
/// Routes incoming socket text (JSON) messages to registered handlers
/// based on the message's <c>Type</c> field.
/// Falls back to a logged warning when no handler matches.
/// </summary>
public sealed class SocketMessageDispatcher(
    IServiceProvider services,
    ILogger<SocketMessageDispatcher> logger
) {
    private const int LogPayloadLimit = 500;

    private static readonly JsonSerializerOptions EnvelopeOptions = new() {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Parses a text message, resolves the matching handler, and dispatches.
    /// Logs a warning if no handler is registered for the message type.
    /// </summary>
    public async Task DispatchAsync(SocketContext context, ReadOnlyMemory<byte> json, CancellationToken ct) {
        string? typeName;
        try {
            typeName = ExtractTypeName(json.Span);
        } catch (JsonException ex) {
            logger.LogWarning(ex, "Received malformed socket JSON: {Payload}", TruncateForLog(json));
            return;
        }

        if (string.IsNullOrEmpty(typeName)) {
            logger.LogWarning("Received socket message without a 'Type' field: {Payload}", TruncateForLog(json));
            return;
        }

        var handler = services.GetKeyedService<ISocketMessageHandler>(typeName);
        if (handler is null) {
            logger.LogWarning("No handler registered for socket message type '{MessageType}'", typeName);
            return;
        }

        try {
            await handler.HandleAsync(context, json, ct);
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            logger.LogError(ex, "Unhandled error in handler for socket message type '{MessageType}'", typeName);
        }
    }

    private static string? ExtractTypeName(ReadOnlySpan<byte> json) {
        var envelope = JsonSerializer.Deserialize<SocketTypeEnvelope>(json, EnvelopeOptions);
        return envelope?.Type;
    }

    private static string TruncateForLog(ReadOnlyMemory<byte> json) {
        var length = Math.Min(json.Length, LogPayloadLimit);
        return Encoding.UTF8.GetString(json.Span[..length]);
    }

    private sealed class SocketTypeEnvelope {
        public string? Type { get; set; }
    }
}
