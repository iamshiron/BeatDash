using System.ComponentModel.DataAnnotations;

namespace Shiron.BeatDash.DB.Schema;

/// <summary>
/// Server-computed analysis for a single <see cref="BeatmapDifficulty"/>, derived
/// by parsing the downloaded map file (as opposed to the client-supplied stats on
/// <see cref="Beatmap"/>/<see cref="BeatmapDifficulty"/>). One row per difficulty.
///
/// <para>For now this holds only simple parsed map data (object counts and parse
/// provenance). It will grow to carry the difficulty rating, PP, characteristic
/// scores and other computed metrics.</para>
/// </summary>
public sealed class BeatmapDifficultyAnalysis {
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Outcome of the analysis attempt. Non-<see cref="BeatmapAnalysisStatus.Success"/>
    /// rows leave the parsed-data fields below null.</summary>
    public required BeatmapAnalysisStatus Status { get; set; }

    // --- Parsed object counts (null unless the attempt succeeded) ---
    public int? NoteCount { get; set; }
    public int? BombCount { get; set; }
    public int? ObstacleCount { get; set; }
    public int? ChainCount { get; set; }
    public int? ArcCount { get; set; }
    public int? BpmChangeCount { get; set; }

    /// <summary>Base song BPM read from the map's info file.</summary>
    public double? Bpm { get; set; }

    /// <summary>Note jump movement speed declared in the map file.</summary>
    public double? Njs { get; set; }

    /// <summary>Note jump start beat offset declared in the map file.</summary>
    public double? NjsOffset { get; set; }

    /// <summary>The beatmap file's on-disk format version (e.g. <c>3.3.0</c>).</summary>
    [MaxLength(32)] public string? FormatVersion { get; set; }

    /// <summary>
    /// Version of the analysis pipeline that produced this row, so results can be
    /// recomputed when the parser/metrics change.
    /// </summary>
    public required int AnalyzerVersion { get; set; }

    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;

    public Guid BeatmapDifficultyId { get; set; }
    public BeatmapDifficulty BeatmapDifficulty { get; set; } = null!;
}
