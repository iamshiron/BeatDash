namespace Shiron.BeatDash.Analysis;

/// <summary>Stable metric identifiers produced by the scoring layer.</summary>
public static class MetricKeys {
    public const string Difficulty = "difficulty";
    public const string Pp = "pp";

    /// <summary>Prefix for characteristic scores, e.g. <c>characteristic.stream</c>.</summary>
    public const string CharacteristicPrefix = "characteristic.";
}

/// <summary>Describes a metric a scorer can produce.</summary>
/// <param name="Key">Stable identifier, e.g. <c>difficulty</c> or <c>characteristic.stream</c>.</param>
/// <param name="Description">Human-readable explanation.</param>
public readonly record struct MetricDefinition(string Key, string Description);

/// <summary>
/// A pluggable unit of scoring. Implementations read the extracted feature set (and
/// any earlier metrics) and write named metric values. Each scorer owns its
/// calibration via the injected <see cref="MetricConfig"/>, so tuning is a config
/// change. Add a scorer + register it in <see cref="MetricScorer.CreateDefault"/> to
/// extend the metric set.
/// </summary>
public interface IMetricScorer {
    /// <summary>Stable name, used for diagnostics when a scorer fails.</summary>
    string Name { get; }

    /// <summary>The metrics this scorer can emit (its contribution to the catalogue).</summary>
    IReadOnlyList<MetricDefinition> Provides { get; }

    /// <summary>
    /// Computes metrics from <paramref name="features"/> into <paramref name="metrics"/>.
    /// May read metrics already produced by earlier scorers (e.g. PP reads difficulty).
    /// </summary>
    void Score(IReadOnlyDictionary<string, double> features, MetricBuilder metrics);
}

/// <summary>Accumulates named metric values during a scoring run.</summary>
public sealed class MetricBuilder {
    private readonly Dictionary<string, double> _values = [];

    public void Set(string key, double value) => _values[key] = value;

    /// <summary>Reads a metric produced by an earlier scorer, or <paramref name="fallback"/> if absent.</summary>
    public double Get(string key, double fallback = 0) => _values.TryGetValue(key, out var v) ? v : fallback;

    internal IReadOnlyDictionary<string, double> Build() => _values;
}

/// <summary>Why a scoring run ended the way it did.</summary>
public enum MetricOutcome {
    /// <summary>All scorers ran and produced metrics.</summary>
    Success,

    /// <summary>A scorer threw an unexpected error.</summary>
    Failed,

    /// <summary>There were no features to score from.</summary>
    NoFeatures,
}

/// <summary>The result of running the scoring pipeline over one feature set.</summary>
/// <param name="Outcome">Overall outcome.</param>
/// <param name="Metrics">Computed metrics (possibly partial/empty on failure).</param>
/// <param name="FailedScorer">Name of the scorer that threw, when <see cref="Outcome"/> is
/// <see cref="MetricOutcome.Failed"/>; otherwise null.</param>
public sealed record MetricResult(
    MetricOutcome Outcome,
    IReadOnlyDictionary<string, double> Metrics,
    string? FailedScorer
) {
    public bool IsSuccess => Outcome == MetricOutcome.Success;

    /// <summary>Returns the characteristic scores (prefix stripped), e.g. <c>stream → 0.42</c>.</summary>
    public IReadOnlyDictionary<string, double> Characteristics() {
        var result = new Dictionary<string, double>();
        foreach (var (key, value) in Metrics) {
            if (key.StartsWith(MetricKeys.CharacteristicPrefix, StringComparison.Ordinal)) {
                result[key[MetricKeys.CharacteristicPrefix.Length..]] = value;
            }
        }
        return result;
    }
}
