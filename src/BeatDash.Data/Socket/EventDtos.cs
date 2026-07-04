namespace Shiron.BeatDash.Data.Socket;

public sealed class NoteEventDto {
    public required int SongTime { get; init; }
    public required int ColorType { get; init; }
    public required int NoteType { get; init; }
    /// <summary>
    /// Beat Saber's <c>NoteData.ScoringType</c> (as <c>int</c>): disambiguates
    /// arc/sliders and chain links that share a <see cref="NoteType"/> of
    /// Normal but have pinned cut-score ranges. Required to reconstruct the
    /// exact per-note score definition.
    /// </summary>
    public required int ScoringType { get; init; }
    public required int CutDirection { get; init; }
    public required int LineIndex { get; init; }
    public required int NoteLineLayer { get; init; }
    public required int Result { get; init; }

    public required int MaxScore { get; init; }

    public required int BeforeCutScore { get; init; }
    public required int CenterDistanceScore { get; init; }
    public required int AfterCutScore { get; init; }

    public required float BeforeCutSwing { get; init; }
    public required float AfterCutSwing { get; init; }

    public required float SaberSpeed { get; init; }
    public required float CutPointDistance { get; init; }
}

public sealed class ComboBreakDto {
    public required int SongTime { get; init; }
    public required int ComboBefore { get; init; }
}

public sealed class EnergyChangeDto {
    public required int SongTime { get; init; }
    public required float Energy { get; init; }
}

/// <summary>
/// A discrete score event carrying the absolute cumulative modified score at
/// the moment the score changed. Used to reconstruct the score curve
/// independently of per-note scoring (anti-cheat/statistics integrity).
/// </summary>
public sealed class ScoreChangeDto {
    public required int SongTime { get; init; }
    public required int Score { get; init; }
}
