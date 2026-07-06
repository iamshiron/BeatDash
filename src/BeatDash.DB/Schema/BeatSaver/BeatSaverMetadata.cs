using System.ComponentModel.DataAnnotations;

namespace Shiron.BeatDash.DB.Schema.BeatSaver;

/// <summary>
/// Song-level metadata reported by BeatSaver, persisted as an owned entity on
/// <see cref="BeatSaverMap"/> (columns on the same table).
/// </summary>
public sealed class BeatSaverMetadata {
    public required float Bpm { get; set; }

    /// <summary>Song duration in whole seconds, as reported by BeatSaver.</summary>
    public required int Duration { get; set; }

    [MaxLength(512)] public required string SongName { get; set; }
    [MaxLength(512)] public string? SongSubName { get; set; }
    [MaxLength(512)] public required string SongAuthorName { get; set; }
    [MaxLength(512)] public required string LevelAuthorName { get; set; }
}
