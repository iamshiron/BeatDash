namespace Shiron.BeatDash.Data.Socket;

/// <summary>
/// Payload sent when a beatmap's gameplay state changes (paused, resumed, finished, failed, or quit).
/// Links to the original <see cref="MapStartMessage"/> via <see cref="CorrelationId"/>.
/// </summary>
public sealed class MapStateMessage : SocketMessage<MapStateMessage> {
    public required int CorrelationId { get; init; }
    public required string LevelId { get; init; }

    /// <summary>
    /// The new gameplay state, serialized from <see cref="MapState"/>.
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// Results data, populated only for <see cref="MapState.Finished"/>,
    /// <see cref="MapState.Failed"/>, and <see cref="MapState.Quit"/>.
    /// Null for <see cref="MapState.Paused"/> and <see cref="MapState.Resumed"/>.
    /// </summary>
    public MapResults? Results { get; init; }
}

/// <summary>
/// Score and performance results for a completed or quit beatmap session.
/// Populated from Beat Saber's <c>LevelCompletionResults</c>.
/// </summary>
public sealed class MapResults {
    /// <summary>Final score after modifier adjustments.</summary>
    public required int Score { get; init; }

    /// <summary>Raw multiplied score before modifier adjustments.</summary>
    public required int MultipliedScore { get; init; }

    /// <summary>Maximum possible multiplied score for this beatmap.</summary>
    public required int MaxMultipliedScore { get; init; }

    /// <summary>Accuracy ratio (0–1): <see cref="MultipliedScore"/> / <see cref="MaxMultipliedScore"/>.</summary>
    public required float Accuracy { get; init; }

    /// <summary>Letter rank (e.g. "S", "SS", "SSS").</summary>
    public required string Rank { get; init; }

    /// <summary>Whether the player never dropped combo.</summary>
    public required bool FullCombo { get; init; }

    /// <summary>Highest combo achieved during the play.</summary>
    public required int MaxCombo { get; init; }

    /// <summary>Number of good cuts.</summary>
    public required int GoodCuts { get; init; }

    /// <summary>Number of bad cuts.</summary>
    public required int BadCuts { get; init; }

    /// <summary>Number of missed notes.</summary>
    public required int MissedNotes { get; init; }

    /// <summary>Saber energy at the end (0–1).</summary>
    public required float Energy { get; init; }

    /// <summary>Song time (seconds) at which the session ended.</summary>
    public required float EndSongTime { get; init; }
}
