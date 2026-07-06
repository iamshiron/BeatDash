namespace Shiron.BeatDash.Analysis.Extractors;

/// <summary>
/// Structure of the note density over time, binned into one-second windows: peak vs
/// typical density (burstiness), and how much of the song is rest.
/// </summary>
public sealed class DensityFeatureExtractor : IFeatureExtractor {
    public const string NpsPeak = "density.nps_peak_1s";
    public const string NpsMedian = "density.nps_median_1s";
    public const string Burstiness = "density.burstiness";
    public const string RestRatio = "density.rest_ratio";

    public string Name => "density";

    public IReadOnlyList<FeatureDefinition> Provides { get; } = [
        new(NpsPeak, "Highest note count in any one-second window"),
        new(NpsMedian, "Median note count across one-second windows"),
        new(Burstiness, "Peak-to-median density ratio (spiky vs sustained)"),
        new(RestRatio, "Fraction of one-second windows with no notes"),
    ];

    public void Extract(FeatureContext context, FeatureBuilder features) {
        var windowCount = Math.Max(1, (int) Math.Ceiling(context.SongLengthSeconds));
        var windows = new int[windowCount];

        foreach (var t in context.NoteTimesSeconds) {
            var idx = Math.Clamp((int) t, 0, windowCount - 1);
            windows[idx]++;
        }

        var asDouble = new double[windowCount];
        var empty = 0;
        for (var i = 0; i < windowCount; i++) {
            asDouble[i] = windows[i];
            if (windows[i] == 0) empty++;
        }

        var peak = FeatureMath.Max(asDouble);
        var median = FeatureMath.Median(asDouble);

        features.Set(NpsPeak, peak);
        features.Set(NpsMedian, median);
        features.Set(Burstiness, peak / Math.Max(median, 1.0));
        features.Set(RestRatio, (double) empty / windowCount);
    }
}
