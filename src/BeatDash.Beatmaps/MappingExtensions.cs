namespace Shiron.BeatDash.Beatmaps;

/// <summary>
/// Helpers for the Mapping Extensions "precision placement" coordinate encoding.
///
/// <para>The parser deliberately preserves coordinates verbatim — decoding is an
/// analysis concern, not a parsing one. A map using precision placement encodes a
/// fractional grid position as an integer of magnitude ≥ 1000
/// (<c>2000 → column 1.0</c>, <c>2500 → 1.5</c>). Consumers that need physical
/// positions should check <see cref="ParsedBeatmap.UsesMappingExtensions"/> and, if
/// set, run each coordinate through <see cref="Decode"/>.</para>
/// </summary>
public static class MappingExtensions {
    private const int PrecisionThreshold = 1000;

    /// <summary>Whether a coordinate is precision-encoded (and thus needs decoding).</summary>
    public static bool IsPrecise(double coordinate) => Math.Abs(coordinate) >= PrecisionThreshold;

    /// <summary>
    /// Decodes a precision-encoded coordinate to its real grid position. Normal and
    /// extended-lane integer positions (magnitude &lt; 1000) pass through unchanged.
    /// </summary>
    public static double Decode(double coordinate) {
        if (coordinate >= PrecisionThreshold) return (coordinate - PrecisionThreshold) / 1000.0;
        if (coordinate <= -PrecisionThreshold) return (coordinate + PrecisionThreshold) / 1000.0;
        return coordinate;
    }
}
