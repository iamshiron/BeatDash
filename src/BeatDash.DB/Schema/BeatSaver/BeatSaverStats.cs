namespace Shiron.BeatDash.DB.Schema.BeatSaver;

/// <summary>
/// Aggregate play/vote statistics reported by BeatSaver, persisted as an owned
/// entity on <see cref="BeatSaverMap"/> (columns on the same table).
/// </summary>
public sealed class BeatSaverStats {
    public required int Plays { get; set; }
    public required int Downloads { get; set; }
    public required int Upvotes { get; set; }
    public required int Downvotes { get; set; }

    /// <summary>BeatSaver's computed approval score (0–1).</summary>
    public required float Score { get; set; }
}
