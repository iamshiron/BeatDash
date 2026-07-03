namespace Shiron.BeatDash.Data.Socket;

public sealed class WallEntryDto {
    public required float StartTime { get; init; }
    public required float Duration { get; init; }
    public required int LineIndex { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
}

public sealed class BombEntryDto {
    public required float SongTime { get; init; }
    public required int LineIndex { get; init; }
    public required int NoteLineLayer { get; init; }
}
