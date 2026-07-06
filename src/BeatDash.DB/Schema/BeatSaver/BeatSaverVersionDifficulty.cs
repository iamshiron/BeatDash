using System.ComponentModel.DataAnnotations;

namespace Shiron.BeatDash.DB.Schema.BeatSaver;

/// <summary>
/// A single difficulty/characteristic entry within a <see cref="BeatSaverVersion"/>,
/// carrying the gameplay stats BeatSaver computes for that beatmap file.
/// </summary>
public sealed class BeatSaverVersionDifficulty {
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required float Njs { get; set; }
    public required float Offset { get; set; }
    public required int Notes { get; set; }
    public required int Bombs { get; set; }
    public required int Obstacles { get; set; }
    public required float Nps { get; set; }

    /// <summary>Beat-space length of the beatmap.</summary>
    public required float Length { get; set; }

    [MaxLength(64)] public required string Characteristic { get; set; }
    [MaxLength(64)] public required string Difficulty { get; set; }

    public required int Events { get; set; }
    public required bool Chroma { get; set; }

    /// <summary>Uses Mapping Extensions (BeatSaver field <c>me</c>).</summary>
    public required bool MappingExtensions { get; set; }

    /// <summary>Uses Noodle Extensions (BeatSaver field <c>ne</c>).</summary>
    public required bool NoodleExtensions { get; set; }

    public required bool Cinema { get; set; }

    /// <summary>Real-time length in seconds.</summary>
    public required float Seconds { get; set; }

    public int? MaxScore { get; set; }
    [MaxLength(128)] public string? Environment { get; set; }

    // Parity summary — folded in as columns.
    public required int ParityErrors { get; set; }
    public required int ParityWarns { get; set; }
    public required int ParityResets { get; set; }

    public Guid BeatSaverVersionId { get; set; }
    public BeatSaverVersion BeatSaverVersion { get; set; } = null!;
}
