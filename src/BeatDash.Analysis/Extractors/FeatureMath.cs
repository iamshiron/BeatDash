namespace Shiron.BeatDash.Analysis.Extractors;

/// <summary>Small numeric helpers shared by the extractors.</summary>
internal static class FeatureMath {
    public static double Mean(IReadOnlyList<double> values) {
        if (values.Count == 0) return 0;
        double sum = 0;
        foreach (var v in values) sum += v;
        return sum / values.Count;
    }

    public static double Max(IReadOnlyList<double> values) {
        if (values.Count == 0) return 0;
        var max = values[0];
        for (var i = 1; i < values.Count; i++) {
            if (values[i] > max) max = values[i];
        }
        return max;
    }

    /// <summary>Linear-interpolated percentile (<paramref name="p"/> in 0..1) over a copy of the data.</summary>
    public static double Percentile(IReadOnlyList<double> values, double p) {
        if (values.Count == 0) return 0;
        if (values.Count == 1) return values[0];

        var sorted = values.ToArray();
        Array.Sort(sorted);

        var rank = p * (sorted.Length - 1);
        var lo = (int) Math.Floor(rank);
        var hi = (int) Math.Ceiling(rank);
        if (lo == hi) return sorted[lo];
        var frac = rank - lo;
        return sorted[lo] * (1 - frac) + sorted[hi] * frac;
    }

    public static double Median(IReadOnlyList<double> values) => Percentile(values, 0.5);
}
