namespace Shiron.BeatDash.Analysis.Scorers;

/// <summary>
/// Base performance points at reference accuracy, driven purely by difficulty:
/// <c>pp = Multiplier · difficulty^Exponent</c>. The accuracy curve is applied
/// separately at score time. Must run after <see cref="DifficultyScorer"/>.
/// </summary>
public sealed class PpScorer(MetricConfig config) : IMetricScorer {
    public string Name => "pp";

    public IReadOnlyList<MetricDefinition> Provides { get; } = [
        new(MetricKeys.Pp, "Base performance points at reference accuracy"),
    ];

    public void Score(IReadOnlyDictionary<string, double> features, MetricBuilder metrics) {
        var difficulty = metrics.Get(MetricKeys.Difficulty);
        metrics.Set(MetricKeys.Pp, config.Pp.Multiplier * Math.Pow(difficulty, config.Pp.Exponent));
    }
}
