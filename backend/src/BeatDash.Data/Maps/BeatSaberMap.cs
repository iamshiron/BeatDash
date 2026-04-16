using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Shiron.BeatDash.Data.Maps.Models;
using Shiron.BeatDash.Data.Maps.Models.Impl;

namespace Shiron.BeatDash.Data.Maps;

public class BeatSaberMap {
    public required IMapInfo Info { get; init; }
    public required IList<IMapDifficulty> Difficulties { get; init; } = [];

    public static async Task<BeatSaberMap> LoadAsync(string folderName) {
        var infoNode = await JsonNode.ParseAsync(File.OpenRead(Path.Combine(folderName, "Info.dat"))) ?? throw new JsonException("Failed to parse Info.dat");
        var infoVersion = infoNode["_version"]?.GetValue<string>();

        var info = infoVersion == "2.0.0" ? V2MapInfo.FromJson(infoNode) : throw new Exception($"Unsupported info.dat version {infoVersion}");
        var difficulties = info.Difficulties.SelectMany(d =>
            V2MapDifficulty.FromJson(JsonNode.Parse(File.OpenRead(Path.Combine(folderName, d.FileName))) ??
                throw new JsonException($"Failed to parse {d.FileName}")));

        return new BeatSaberMap {
            Info = info,
            Difficulties = difficulties.ToList()
        };
    }
}
