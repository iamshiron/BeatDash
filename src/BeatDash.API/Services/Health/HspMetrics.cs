namespace Shiron.BeatDash.API.Services.Health;

/// <summary>
/// Canonical registry of the metrics the Honami Sensor Proxy accepts: their wire names, canonical
/// units, and plausible value ranges. Unknown metrics and out-of-range values are dropped per
/// sample at ingest (never a whole-request failure).
/// </summary>
public static class HspMetrics {
    public const string HeartRate = "heart_rate";
    public const string Calories = "calories";
    public const string Steps = "steps";
    public const string SpO2 = "spo2";

    public readonly record struct MetricSpec(string Unit, double Min, double Max);

    private static readonly IReadOnlyDictionary<string, MetricSpec> Registry =
        new Dictionary<string, MetricSpec>(StringComparer.OrdinalIgnoreCase) {
            [HeartRate] = new("bpm", 20, 240),
            [Calories] = new("kcal", 0, 100),
            [Steps] = new("count", 0, 10_000),
            [SpO2] = new("percent", 50, 100)
        };

    /// <summary>All metric names advertised to clients during provisioning.</summary>
    public static IReadOnlyCollection<string> Known { get; } = [.. Registry.Keys];

    /// <summary>True when the metric is known and the value is finite and within its plausible range.</summary>
    public static bool IsValid(string metric, double value) =>
        Registry.TryGetValue(metric, out var spec)
        && double.IsFinite(value)
        && value >= spec.Min && value <= spec.Max;

    /// <summary>The canonical unit for a known metric, or null.</summary>
    public static string? CanonicalUnit(string metric) =>
        Registry.TryGetValue(metric, out var spec) ? spec.Unit : null;
}
