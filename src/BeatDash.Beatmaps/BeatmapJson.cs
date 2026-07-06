using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shiron.BeatDash.Beatmaps;

/// <summary>
/// Canonical JSON serialization of the unified beatmap model. Property names are
/// emitted as <c>snake_case</c> (e.g. <c>angle_offset</c>, <c>bpm_changes</c>) so
/// the output is stable and easy to diff against other tooling.
/// </summary>
public static class BeatmapJson {
    private static readonly JsonSerializerOptions Compact = new() {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false,
    };

    private static readonly JsonSerializerOptions Indented = new(Compact) { WriteIndented = true };

    private static readonly JsonSerializerOptions SummaryOptions = new() { WriteIndented = false };

    /// <summary>Serializes the full unified level (all objects) to a compact single line.</summary>
    public static string Serialize(ParsedLevel level) => JsonSerializer.Serialize(level, Compact);

    /// <summary>Serializes the full unified level, pretty-printed.</summary>
    public static string SerializeIndented(ParsedLevel level) => JsonSerializer.Serialize(level, Indented);

    /// <summary>
    /// Serializes a lightweight per-map summary (metadata + per-difficulty object
    /// counts) to a compact single line — handy for scanning a whole collection.
    /// </summary>
    public static string SerializeSummary(ParsedLevel level) {
        var summary = new {
            folder = level.Folder,
            song_name = level.SongName,
            song_author = level.SongAuthor,
            mapper = level.Mapper,
            bpm = level.Bpm,
            info_version = level.InfoVersion,
            difficulties = level.Beatmaps.Select(b => new {
                characteristic = b.Characteristic,
                difficulty = b.Difficulty,
                format_version = b.FormatVersion,
                counts = b.Counts,
            }),
        };
        return JsonSerializer.Serialize(summary, SummaryOptions);
    }
}
