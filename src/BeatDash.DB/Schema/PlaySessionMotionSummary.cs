namespace Shiron.BeatDash.DB.Schema;

/// <summary>
/// Server-computed scalar metrics derived from a play's motion frames, produced
/// once at finalization (mirrors the offline-analysis side-table pattern of
/// <see cref="BeatmapDifficultyAnalysis"/>). One nullable 1:1 row per
/// <see cref="PlaySession"/>; absent when the play had no motion data.
/// </summary>
public sealed class PlaySessionMotionSummary {
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid PlaySessionId { get; set; }
    public PlaySession PlaySession { get; set; } = null!;

    /// <summary>Number of motion frames the metrics were computed from.</summary>
    public required int FrameCount { get; set; }

    /// <summary>Nominal decimation rate of the frames (Hz).</summary>
    public required int SampleRateHz { get; set; }

    // --- Travel (metres of cumulative position change over the play) ---
    public required double LeftSaberTravel { get; set; }
    public required double RightSaberTravel { get; set; }

    /// <summary>Head cumulative travel — a proxy for dodge/lean intensity.</summary>
    public required double HeadTravel { get; set; }

    // --- Average speed (m/s over the song span) ---
    public required double AvgLeftSaberSpeed { get; set; }
    public required double AvgRightSaberSpeed { get; set; }

    // --- Reach range (bounding-box diagonal of positions, metres) ---
    public required double LeftReachRange { get; set; }
    public required double RightReachRange { get; set; }
    public required double HeadRange { get; set; }

    /// <summary>
    /// Fatigue curve as a JSON array of <c>{tMs,leftSpeed,rightSpeed}</c> points
    /// (saber speed bucketed over song time), stored as <c>jsonb</c>. A downward
    /// trend indicates tiring.
    /// </summary>
    public required string FatigueCurve { get; set; }
}
