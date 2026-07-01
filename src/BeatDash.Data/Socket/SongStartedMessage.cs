namespace Shiron.BeatDash.Data.Socket;

public class SongStartedMessage : SocketMessage<SongStartedMessage> {
    public required string SongName { get; init; }
}
