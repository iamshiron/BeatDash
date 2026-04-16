using System.Text.Json.Nodes;

namespace Shiron.BeatDash.Data.Maps.Models.Impl;

public class V2MapDifficulty : IMapDifficulty {
    public static ICollection<IMapDifficulty> FromJson(JsonNode json) {
        var notes = json["_notes"]?.AsArray() ?? [];
        return notes.OfType<JsonNode>().Select(IMapDifficulty (note) => new V2MapDifficulty {
            Time = note.GetValueDecimal("_time"),
            LineIndex = note.GetValueDecimal("_lineIndex"),
            LineLayer = note.GetValueDecimal("_lineLayer"),
            Type = note.GetValueDecimal("_type"),
            CutDirection = note.GetValueDecimal("_cutDirection")
        }).ToList();
    }

    public required decimal Time { get; init; }
    public required decimal LineIndex { get; init; }
    public required decimal LineLayer { get; init; }
    public required decimal Type { get; init; }
    public required decimal CutDirection { get; init; }
}
