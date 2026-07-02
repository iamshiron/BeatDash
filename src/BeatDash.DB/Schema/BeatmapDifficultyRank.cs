namespace Shiron.BeatDash.DB.Schema;

/// <summary>
/// The ranked difficulty of a beatmap, mirroring the fixed set of values
/// reported by the game client.
/// </summary>
public enum BeatmapDifficultyRank {
    Easy,
    Normal,
    Hard,
    Expert,
    ExpertPlus
}
