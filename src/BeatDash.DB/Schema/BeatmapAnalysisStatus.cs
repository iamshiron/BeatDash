namespace Shiron.BeatDash.DB.Schema;

/// <summary>
/// Outcome of a <see cref="BeatmapDifficultyAnalysis"/> attempt. A row is written
/// for every attempted difficulty, so failures are recorded rather than silently
/// dropped.
/// </summary>
public enum BeatmapAnalysisStatus {
    /// <summary>Parsed and analyzed successfully.</summary>
    Success = 0,

    /// <summary>A general/unexpected error occurred.</summary>
    Failed = 1,

    /// <summary>No downloaded map zip was available to analyze.</summary>
    ZipMissing = 2,

    /// <summary>The map file could not be parsed.</summary>
    ParseFailed = 3,

    /// <summary>The map parsed, but this difficulty was not present in it.</summary>
    DifficultyNotFound = 4,
}
