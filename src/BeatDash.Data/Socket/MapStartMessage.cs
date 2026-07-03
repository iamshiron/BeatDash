namespace Shiron.BeatDash.Data.Socket;

/// <summary>
/// Payload sent when a beatmap starts, containing map metadata and gameplay stats.
/// </summary>
public sealed class MapStartMessage : SocketMessage<MapStartMessage> {
    public required int CorrelationId { get; init; }
    public required string LevelId { get; init; }
    public required int DurationMs { get; init; }
    public required float NotesPerSecond { get; init; }
    public required string SongName { get; init; }
    public required string SongSubName { get; init; }
    public required string SongAuthor { get; init; }
    public required string Mapper { get; init; }
    public required float Bpm { get; init; }
    public required string Difficulty { get; init; }
    public required string DifficultyName { get; init; }
    public required float? NoteJumpSpeed { get; init; }
    public required int BombCount { get; init; }
    public required int ObstacleCount { get; init; }
    public required int CuttableObjectCount { get; init; }
    public required int LaneCount { get; init; }
    public required BeatmapCharacteristic Characteristic { get; init; }

    /// <summary>
    /// Packed bitmask of all gameplay modifiers. See <see cref="ModifierBit"/> for bit positions.
    /// </summary>
    public required int ModifierFlags { get; init; }

    public required float SongSpeed { get; init; }
    public required int NotesPerHandLeft { get; init; }
    public required int NotesPerHandRight { get; init; }
    public required int[] NpsCurve { get; init; }
    public required WallEntryDto[] WallTimeline { get; init; }
    public required BombEntryDto[] BombPositions { get; init; }
}

/// <summary>
/// Describes the beatmap characteristic (game mode) associated with a map.
/// </summary>
public sealed class BeatmapCharacteristic {
    public required int ColorCount { get; init; }
    public required bool Requires360Movement { get; init; }
    public required bool ContainsRotationEvents { get; init; }
    public required string SerializedName { get; init; }
    public required string LocalizationKey { get; init; }
    public required string DescriptionLocalizationKey { get; init; }
}
