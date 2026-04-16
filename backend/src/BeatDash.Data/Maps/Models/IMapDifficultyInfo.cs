namespace Shiron.BeatDash.Data.Maps.Models;

public interface IMapDifficultyInfo {
    string Name { get; }
    int Rank { get; }
    string FileName { get; }
    int NoteJumpMovementSpeed { get; }
    int NoteJumpStartBeatOffset { get; }
}
