namespace Shiron.BeatDash.API.Configuration;

/// <summary>
/// Motion frame ingestion gates. Bound from the "MotionFrame" configuration section.
/// </summary>
public sealed class MotionFrameOptions {
    /// <summary>
    /// Target sample rate (Hz) the server decimates incoming frames down to.
    /// </summary>
    public int TargetHz { get; set; } = 30;

    /// <summary>
    /// Hard cap on the frame count a single packet may claim. Larger batches are
    /// dropped outright as an abuse guard.
    /// </summary>
    public int MaxFramesPerPacket { get; set; } = 2048;

    /// <summary>
    /// Minimum wall-clock gap (ms) between two accepted packets for the same play.
    /// Packets arriving faster are dropped as a flood guard.
    /// </summary>
    public int MinPacketGapMs { get; set; } = 100;
}
