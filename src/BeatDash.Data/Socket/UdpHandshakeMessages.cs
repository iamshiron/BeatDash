using System;

namespace Shiron.BeatDash.Data.Socket;

/// <summary>
/// Sent by the server over the TCP WebSocket right after a client connects.
/// Carries a single-use ticket the client must present over UDP to bind its
/// endpoint, plus the UDP port it should holepunch to.
/// </summary>
public sealed class UdpHandshakeMessage : SocketMessage<UdpHandshakeMessage> {
    public required Guid Ticket { get; init; }
    public required int Port { get; init; }
}

/// <summary>
/// Sent by the server over the TCP WebSocket once a client's UDP holepunch has
/// been accepted and its endpoint bound. Receiving this tells the client that UDP
/// is available and it may send binary packets over UDP instead of TCP.
/// </summary>
public sealed class UdpBoundMessage : SocketMessage<UdpBoundMessage> {
}
