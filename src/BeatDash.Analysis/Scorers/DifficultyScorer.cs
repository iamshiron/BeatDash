namespace Shiron.BeatDash.Analysis.Scorers;

/// <summary>Overall difficulty rating in <c>[0,1]</c>, a normalized weighted blend of features.</summary>
public sealed class DifficultyScorer(MetricConfig config) : IMetricScorer {
    public string Name => "difficulty";

    public IReadOnlyList<MetricDefinition> Provides { get; } = [
        new(MetricKeys.Difficulty, "Overall difficulty rating in [0,1]"),
    ];

    public void Score(IReadOnlyDictionary<string, double> features, MetricBuilder metrics) =>
        metrics.Set(MetricKeys.Difficulty, config.Difficulty.Normalized(features));
}
