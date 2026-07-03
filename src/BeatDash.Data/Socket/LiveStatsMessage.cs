namespace Shiron.BeatDash.Data.Socket;

/// <summary>
/// Cumulative per-hand statistics accumulated during gameplay.
/// ColorA (left saber) and ColorB (right saber) are tracked independently.
/// All counts and score totals are cumulative from the start of the session.
/// </summary>
public sealed class HandStatsDto {
    public required int GoodCuts { get; init; }
    public required int BadCuts { get; init; }
    public required int Misses { get; init; }

    public required int TotalBeforeCutScore { get; init; }
    public required int TotalCenterDistanceScore { get; init; }
    public required int TotalAfterCutScore { get; init; }

    /// <summary>Average pre-swing rating across all good cuts for this hand (0–1).</summary>
    public required float AverageBeforeCutSwing { get; init; }

    /// <summary>Average post-swing rating across all good cuts for this hand (0–1).</summary>
    public required float AverageAfterCutSwing { get; init; }
}

/// <summary>
/// Periodic snapshot of live gameplay statistics sent at a fixed interval.
/// The <see cref="SongTime"/> field is in the original song's timeline coordinates
/// (unaffected by speed modifiers), making it suitable as a chart x-axis.
/// Pauses are invisible because <c>SongTime</c> stops advancing during pause.
/// </summary>
public sealed class LiveStatsMessage : SocketMessage<LiveStatsMessage> {
    public required int CorrelationId { get; init; }

    /// <summary>Position in the original song timeline (seconds). Pauses stop advancement.</summary>
    public required float SongTime { get; init; }

    /// <summary>Current cumulative multiplied score (before modifier adjustments).</summary>
    public required int Score { get; init; }

    /// <summary>Current cumulative modified score (after modifier score multipliers).</summary>
    public required int ModifiedScore { get; init; }

    /// <summary>Maximum possible multiplied score at this point in the song.</summary>
    public required int MaxPossibleScore { get; init; }

    /// <summary>Saber energy / health (0–1).</summary>
    public required float Energy { get; init; }

    /// <summary>Current active combo (resets on miss/bad cut).</summary>
    public required int CurrentCombo { get; init; }

    /// <summary>Highest combo achieved so far.</summary>
    public required int MaxCombo { get; init; }

    public required HandStatsDto LeftHand { get; init; }
    public required HandStatsDto RightHand { get; init; }

    public NoteEventDto[] NoteEvents { get; init; } = [];
    public ComboBreakDto[] ComboBreaks { get; init; } = [];
    public EnergyChangeDto[] EnergyChanges { get; init; } = [];
}
