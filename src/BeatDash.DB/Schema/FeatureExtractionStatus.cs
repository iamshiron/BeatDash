namespace Shiron.BeatDash.DB.Schema;

/// <summary>
/// Outcome of the feature-extraction stage for a <see cref="BeatmapDifficultyAnalysis"/>.
/// Explains why extraction failed when it did.
/// </summary>
public enum FeatureExtractionStatus {
    /// <summary>Extraction has not run (e.g. the difficulty never parsed successfully).</summary>
    NotAttempted = 0,

    /// <summary>Features were extracted successfully.</summary>
    Success = 1,

    /// <summary>A general/unexpected error occurred while extracting.</summary>
    Failed = 2,

    /// <summary>The difficulty had no notes to extract features from.</summary>
    NoNotes = 3,

    /// <summary>The map's timing was invalid (non-positive BPM); beats can't be mapped to time.</summary>
    InvalidTiming = 4,
}
