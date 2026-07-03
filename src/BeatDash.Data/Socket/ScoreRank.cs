namespace Shiron.BeatDash.Data.Socket;

/// <summary>
/// Computes Beat Saber letter grades from accuracy ratios.
/// </summary>
public static class ScoreRank {
    /// <summary>
    /// Returns the letter grade for the given accuracy.
    /// </summary>
    /// <param name="accuracy">Accuracy ratio (0–1).</param>
    /// <returns>One of: SS, S, A, B, C, D, E.</returns>
    public static string FromAccuracy(float accuracy) {
        if (accuracy >= 0.90f) return "SS";
        if (accuracy >= 0.80f) return "S";
        if (accuracy >= 0.65f) return "A";
        if (accuracy >= 0.50f) return "B";
        if (accuracy >= 0.35f) return "C";
        if (accuracy >= 0.20f) return "D";
        return "E";
    }
}
