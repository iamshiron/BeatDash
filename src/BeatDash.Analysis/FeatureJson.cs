using System.Text.Json;

namespace Shiron.BeatDash.Analysis;

/// <summary>Serializes an extracted feature set to a stable, compact JSON object.</summary>
public static class FeatureJson {
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    /// <summary>Serializes features as a JSON object with keys sorted for stable output.</summary>
    public static string Serialize(IReadOnlyDictionary<string, double> features) {
        var ordered = new SortedDictionary<string, double>(StringComparer.Ordinal);
        foreach (var (key, value) in features) ordered[key] = value;
        return JsonSerializer.Serialize(ordered, Options);
    }
}
