namespace Shiron.BeatDash.Data.Socket;

public class MapStartMessage : SocketMessage<MapStartMessage> {
    public required string LevelId { get; set; }
    public required int DurationMs { get; set; }
    public required float NotesPerSecond { get; set; }
    public required string SongName { get; set; }
    public required string SongSubName { get; set; }
    public required string SongAuthor { get; set; }
    public required string Mapper { get; set; }
    public required float Bpm { get; set; }
    public required string Difficulty { get; set; }
    public required float? NoteJumpSpeed { get; set; }
    public required int BombsCount { get; set; }
    public required int ObstaclesCount { get; set; }
    public required int CuttableObjectsCount { get; set; }
    public required int LaneCount { get; set; }

    public required BeatmapCharacteristic Characteristic { get; set; }
}

public class BeatmapCharacteristic {
    public required int NumberOfColors { get; set; }
    public required bool Requires360Movement { get; set; }
    public required bool ContainsRotationEvents { get; set; }
    public required string SerializedName { get; set; }
    public required string LocalizationKey { get; set; }
    public required string DescriptionLocalizationKey { get; set; }
}
