namespace Shiron.BeatDash.Data.Socket;

/// <summary>
/// Lightweight score snapshot sent on every note scoring event (including misses)
/// for real-time dashboard animations.
/// </summary>
public sealed class ScoreUpdateMessage : SocketMessage<ScoreUpdateMessage> {
    public required int CorrelationId { get; init; }

    /// <summary>Position in the original song timeline (seconds).</summary>
    public required float SongTime { get; init; }

    /// <summary>Current cumulative modified score (all multipliers and modifiers applied).</summary>
    public required int Score { get; init; }

    /// <summary>Maximum possible modified score at this point in the song.</summary>
    public required int MaxScore { get; init; }

    /// <summary>Accuracy ratio (0–1): Score / MaxScore.</summary>
    public required float Accuracy { get; init; }

    /// <summary>Letter grade computed from accuracy: SS, S, A, B, C, D, E.</summary>
    public required string Rank { get; init; }

    /// <summary>Saber energy / health (0–1).</summary>
    public required float Energy { get; init; }

    /// <summary>Current active combo (resets on miss/bad cut).</summary>
    public required int Combo { get; init; }
}
