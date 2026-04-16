using System.Text.Json.Nodes;

namespace Shiron.BeatDash.Data.Maps.Models.Impl;

public class V2MapDifficultyInfo : IMapDifficultyInfo {
    public static IMapDifficultyInfo FromJson(JsonNode? json) {
        if (json == null) throw new ArgumentException("Json cannot be null", nameof(json));

        return new V2MapDifficultyInfo {
            Name = json.GetValueString("_difficulty"),
            Rank = json.GetValueInt("_difficultyRank"),
            FileName = json.GetValueString("_beatmapFilename"),
            NoteJumpMovementSpeed = json.GetValueInt("_noteJumpMovementSpeed"),
            NoteJumpStartBeatOffset = json.GetValueInt("_noteJumpStartBeatOffset")
        };
    }

    public required string Name { get; init; }
    public required int Rank { get; init; }
    public required string FileName { get; init; }
    public required int NoteJumpMovementSpeed { get; init; }
    public required int NoteJumpStartBeatOffset { get; init; }
}
