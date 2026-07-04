namespace Shiron.BeatDash.DB.Schema;

/// <summary>
/// A complete play's motion data, stored as a single Brotli-compressed
/// blob. <see cref="FrameCount"/> 30Hz samples are packed back-to-back, each
/// sample holding the left saber, right saber, then head transforms (7 floats
/// each: PosX, PosY, PosZ, RotX, RotY, RotZ, RotW). Inherited
/// <see cref="PlaySessionItem.SongTimeMs"/> holds the last sampled song time.
/// </summary>
public class PlaySessionItemMotionFrame : PlaySessionItem {
    /// <summary>
    /// Brotli-compressed motion samples. Decompressed layout is
    /// <see cref="FrameCount"/> * 21 little-endian single-precision floats.
    /// </summary>
    public required byte[] Data { get; set; }

    /// <summary>
    /// Number of 30Hz frames stored in <see cref="Data"/>.
    /// </summary>
    public required int FrameCount { get; set; }
}
