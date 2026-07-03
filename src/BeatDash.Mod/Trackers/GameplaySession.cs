namespace Shiron.BeatDash.Mod.Trackers;

/// <summary>
/// Shared per-session state, populated by <see cref="GameplaySessionTracker"/>
/// during initialization and read by <see cref="LiveStatsTracker"/> during gameplay.
/// </summary>
public sealed class GameplaySession {
    public int CorrelationId { get; set; }
    public string LevelId { get; set; } = "";
    public int MaxMultipliedScore { get; set; }
    public bool IsInitialized { get; set; }
}
