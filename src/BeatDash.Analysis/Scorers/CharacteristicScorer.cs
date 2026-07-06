namespace Shiron.BeatDash.Analysis.Scorers;

/// <summary>
/// Emits one <c>[0,1]</c> score per configured characteristic (Stream, Tech, Speed,
/// Jumps, Gimmick, …). The set of characteristics is entirely config-driven — add a
/// key to <see cref="MetricConfig.Characteristics"/> and it is scored and reported
/// with no code change.
/// </summary>
public sealed class CharacteristicScorer(MetricConfig config) : IMetricScorer {
    public string Name => "characteristics";

    public IReadOnlyList<MetricDefinition> Provides { get; } = config.Characteristics.Keys
        .Select(k => new MetricDefinition(MetricKeys.CharacteristicPrefix + k, $"'{k}' characteristic score in [0,1]"))
        .ToList();

    public void Score(IReadOnlyDictionary<string, double> features, MetricBuilder metrics) {
        foreach (var (name, model) in config.Characteristics) {
            metrics.Set(MetricKeys.CharacteristicPrefix + name, model.Normalized(features));
        }
    }
}
