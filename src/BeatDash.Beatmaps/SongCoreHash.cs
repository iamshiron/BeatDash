using System.Security.Cryptography;
using System.Text.Json;

namespace Shiron.BeatDash.Beatmaps;

/// <summary>
/// Computes the SongCore/BeatSaver hash of a custom level — the SHA1 of the raw
/// <c>Info.dat</c> bytes followed by each difficulty file's raw bytes, in the order
/// they are referenced in <c>Info.dat</c> — and the game <c>levelID</c> derived from
/// it. This matches the identifier the game/mod reports, so a hash computed here is
/// fetch-compatible and dedups against real in-game plays.
/// </summary>
public static class SongCoreHash {
    private const string LevelIdPrefix = "custom_level_";

    /// <summary>The game level id: <c>custom_level_{HASH}</c> (uppercase hex).</summary>
    public static string LevelId(IBeatmapFileSource source) => LevelIdPrefix + Compute(source);

    /// <summary>The uppercase-hex SongCore hash of the level.</summary>
    public static string Compute(IBeatmapFileSource source) {
        var infoBytes = source.TryReadFile("Info.dat")
            ?? throw new UnknownFormatException($"No Info.dat in '{source.Name}'");

        var difficultyFiles = ExtractDifficultyFilenames(infoBytes);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        hash.AppendData(infoBytes);
        foreach (var filename in difficultyFiles) {
            var bytes = source.TryReadFile(filename);
            if (bytes is not null) hash.AppendData(bytes);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    /// <summary>Reads the difficulty filenames in Info.dat order (v2 and v4 layouts).</summary>
    private static List<string> ExtractDifficultyFilenames(byte[] infoBytes) {
        // Strip a UTF-8 BOM for JSON parsing only; the hash still uses the raw bytes.
        var start = infoBytes.Length >= 3 && infoBytes[0] == 0xEF && infoBytes[1] == 0xBB && infoBytes[2] == 0xBF ? 3 : 0;
        using var doc = JsonDocument.Parse(new ReadOnlyMemory<byte>(infoBytes, start, infoBytes.Length - start));
        var root = doc.RootElement;
        var files = new List<string>();

        var version = GetString(root, "_version") ?? GetString(root, "version");
        if (IsV4(version)) {
            foreach (var d in EnumerateArray(root, "difficultyBeatmaps")) {
                if (GetString(d, "beatmapDataFilename") is { } fn) files.Add(fn);
            }
        } else {
            foreach (var set in EnumerateArray(root, "_difficultyBeatmapSets")) {
                foreach (var d in EnumerateArray(set, "_difficultyBeatmaps")) {
                    if (GetString(d, "_beatmapFilename") is { } fn) files.Add(fn);
                }
            }
        }

        return files;
    }

    private static bool IsV4(string? version) =>
        version is not null && int.TryParse(version.Split('.')[0], out var major) && major == 4;

    private static IEnumerable<JsonElement> EnumerateArray(JsonElement obj, string key) {
        if (obj.ValueKind == JsonValueKind.Object
            && obj.TryGetProperty(key, out var v)
            && v.ValueKind == JsonValueKind.Array) {
            foreach (var e in v.EnumerateArray()) yield return e;
        }
    }

    private static string? GetString(JsonElement obj, string key) =>
        obj.ValueKind == JsonValueKind.Object
        && obj.TryGetProperty(key, out var v)
        && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
