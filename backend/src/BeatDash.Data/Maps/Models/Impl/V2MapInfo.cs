using System.Text.Json.Nodes;

namespace Shiron.BeatDash.Data.Maps.Models.Impl;

public class V2MapInfo : IMapInfo {
    public static V2MapInfo FromJson(JsonNode json) {
        return new V2MapInfo {
            SongName = json.GetValueString("_songName"),
            SongSubName = json.GetValueString("_songSubName"),
            SongAuthorName = json.GetValueString("_songAuthorName"),
            LevelAuthorName = json.GetValueString("_levelAuthorName"),
            BeatsPerMinute = json.GetValueInt("_beatsPerMinute"),
            SongFileName = json.GetValueString("_songFilename"),
            CoverImageFileName = json.GetValueString("_coverImageFilename"),
            Difficulties = json["_difficultyBeatmapSets"].AsArray()
                .SelectMany(a => a["_difficultyBeatmaps"].AsArray()
                    .Select(V2MapDifficultyInfo.FromJson)).ToList()
        };
    }

    public required string SongName { get; init; }
    public required string SongSubName { get; init; }
    public required string SongAuthorName { get; init; }
    public required string LevelAuthorName { get; init; }
    public required int BeatsPerMinute { get; init; }
    public required string SongFileName { get; init; }
    public required string CoverImageFileName { get; init; }
    public required IList<IMapDifficultyInfo> Difficulties { get; init; }
}
