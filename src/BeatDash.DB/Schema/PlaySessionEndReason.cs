namespace Shiron.BeatDash.DB.Schema;

/// <summary>
/// How a <see cref="PlaySession"/> ended. Captured from the client's terminal
/// <c>MapState</c> message (Finished/Failed/Quit); <see cref="Incomplete"/>
/// marks sessions ended without a terminal state (e.g. socket disconnect).
/// </summary>
public enum PlaySessionEndReason {
    Incomplete = 0,
    Finished = 1,
    Failed = 2,
    Quit = 3,
}
