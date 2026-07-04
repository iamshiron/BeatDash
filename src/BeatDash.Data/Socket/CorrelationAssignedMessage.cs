namespace Shiron.BeatDash.Data.Socket;

/// <summary>
/// Sent by the server over the TCP WebSocket in response to a
/// <see cref="MapStartMessage"/>, carrying the server-authoritative correlation
/// ID that the client must stamp onto every subsequent packet for that map.
/// </summary>
public sealed class CorrelationAssignedMessage : SocketMessage<CorrelationAssignedMessage> {
    public required int CorrelationId { get; init; }
}
