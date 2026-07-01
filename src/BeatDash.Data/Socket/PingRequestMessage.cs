namespace Shiron.BeatDash.Data.Socket;

public class PingRequestMessage : SocketMessage<PingRequestMessage> {
    public required string Message { get; init; }
}
