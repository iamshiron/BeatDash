namespace Shiron.BeatDash.Analysis;

/// <summary>
/// Describes a single feature an extractor can produce. Extractors declare these
/// so the full feature catalogue is discoverable (data-driven), independent of
/// which values a given map actually yields.
/// </summary>
/// <param name="Key">Stable dotted identifier, e.g. <c>swing.hand_speed_mean</c>.</param>
/// <param name="Description">Human-readable explanation of the feature.</param>
public readonly record struct FeatureDefinition(string Key, string Description);

/// <summary>
/// A pluggable unit of feature extraction. Implementations are pure: they read the
/// shared <see cref="FeatureContext"/> and write named scalar features into the
/// <see cref="FeatureBuilder"/>. Add a new extractor + register it in
/// <see cref="FeatureExtractor.CreateDefault"/> to extend the feature set — no other
/// code changes required.
/// </summary>
public interface IFeatureExtractor {
    /// <summary>Stable name, used for diagnostics when an extractor fails.</summary>
    string Name { get; }

    /// <summary>The features this extractor can emit (its contribution to the catalogue).</summary>
    IReadOnlyList<FeatureDefinition> Provides { get; }

    /// <summary>Computes features from <paramref name="context"/> into <paramref name="features"/>.</summary>
    void Extract(FeatureContext context, FeatureBuilder features);
}

/// <summary>Accumulates named scalar features during an extraction run.</summary>
public sealed class FeatureBuilder {
    private readonly Dictionary<string, double> _values = [];

    /// <summary>Sets (or overwrites) a feature value.</summary>
    public void Set(string key, double value) => _values[key] = value;

    /// <summary>Sets a feature only when a value is present (keeps unknowns absent).</summary>
    public void SetIf(string key, double? value) {
        if (value.HasValue) _values[key] = value.Value;
    }

    internal IReadOnlyDictionary<string, double> Build() => _values;
}

/// <summary>Why a feature-extraction run ended the way it did.</summary>
public enum FeatureExtractionOutcome {
    /// <summary>All extractors ran and produced features.</summary>
    Success,

    /// <summary>An extractor threw an unexpected error.</summary>
    Failed,

    /// <summary>The difficulty had no notes to extract features from.</summary>
    NoNotes,

    /// <summary>Timing was invalid (non-positive BPM); beats can't be mapped to time.</summary>
    InvalidTiming,
}

/// <summary>The result of running the feature pipeline over one difficulty.</summary>
/// <param name="Outcome">Overall outcome.</param>
/// <param name="Features">Extracted features (possibly partial/empty on failure).</param>
/// <param name="FailedExtractor">Name of the extractor that threw, when <see cref="Outcome"/> is
/// <see cref="FeatureExtractionOutcome.Failed"/>; otherwise null.</param>
public sealed record FeatureExtractionResult(
    FeatureExtractionOutcome Outcome,
    IReadOnlyDictionary<string, double> Features,
    string? FailedExtractor
) {
    public bool IsSuccess => Outcome == FeatureExtractionOutcome.Success;
}
