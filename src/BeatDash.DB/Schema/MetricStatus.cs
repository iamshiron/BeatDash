namespace Shiron.BeatDash.DB.Schema;

/// <summary>
/// Outcome of the metric-scoring stage (difficulty, PP, characteristics) for a
/// <see cref="BeatmapDifficultyAnalysis"/>. Explains why scoring failed when it did.
/// </summary>
public enum MetricStatus {
    /// <summary>Scoring has not run (e.g. features were never extracted).</summary>
    NotAttempted = 0,

    /// <summary>Metrics were computed successfully.</summary>
    Success = 1,

    /// <summary>A general/unexpected error occurred while scoring.</summary>
    Failed = 2,

    /// <summary>There were no features to score from.</summary>
    NoFeatures = 3,
}
