namespace Shiron.BeatDash.Data.Socket;

/// <summary>
/// Represents a gameplay lifecycle state change for a beatmap session.
/// </summary>
public enum MapState {
    Paused,
    Resumed,
    Finished,
    Failed,
    Quit,
}
