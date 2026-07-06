using Shiron.BeatDash.Analysis.Scorers;

namespace Shiron.BeatDash.Analysis;

/// <summary>
/// Runs a set of <see cref="IMetricScorer"/> plugins over an extracted feature set to
/// produce the final metrics (difficulty, PP, characteristic scores). Scorers run in
/// order, so later ones may read earlier metrics (PP reads difficulty). Each scorer is
/// isolated so one failure yields a diagnosable result rather than a crash.
/// </summary>
public sealed class MetricScorer {
    private static readonly IReadOnlyDictionary<string, double> Empty = new Dictionary<string, double>();

    private readonly IReadOnlyList<IMetricScorer> _scorers;

    public MetricScorer(IEnumerable<IMetricScorer> scorers) {
        _scorers = scorers.ToList();
    }

    /// <summary>
    /// The default pipeline, calibrated by <paramref name="config"/>. This ordered list
    /// is the single place scorers are registered — add one here to extend the metrics.
    /// </summary>
    public static MetricScorer CreateDefault(MetricConfig config) => new([
        new DifficultyScorer(config),
        new PpScorer(config),
        new CharacteristicScorer(config),
    ]);

    /// <summary>Every metric the configured scorers can emit.</summary>
    public IReadOnlyList<MetricDefinition> Catalog =>
        _scorers.SelectMany(s => s.Provides).ToList();

    /// <summary>Scores a feature set produced by the feature extractor.</summary>
    public MetricResult Score(IReadOnlyDictionary<string, double> features) {
        if (features.Count == 0) return new MetricResult(MetricOutcome.NoFeatures, Empty, null);

        var builder = new MetricBuilder();
        foreach (var scorer in _scorers) {
            try {
                scorer.Score(features, builder);
            } catch {
                return new MetricResult(MetricOutcome.Failed, builder.Build(), scorer.Name);
            }
        }

        return new MetricResult(MetricOutcome.Success, builder.Build(), null);
    }
}
