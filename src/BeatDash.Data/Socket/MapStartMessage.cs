namespace Shiron.BeatDash.Data.Socket;

public class MapStartMessage : SocketMessage<MapStartMessage> {
    public required string SongName { get; set; }
    public required string SongSubName { get; set; }
    public required string SongAuthor { get; set; }
    public required string Mapper { get; set; }
    public required float BPM { get; set; }
    public required string Difficulty { get; set; }
    public required float NJS { get; set; }
}
