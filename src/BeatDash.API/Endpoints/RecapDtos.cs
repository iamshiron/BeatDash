namespace Shiron.BeatDash.API.Endpoints;

/// <summary>
/// Post-session recap: the finished session plus how it compares to the player's
/// previous attempt and their average on the same difficulty.
/// </summary>
public sealed record SessionRecapDto(
    PlaySessionDetailDto Session,
    PlaySessionResultsDto? PreviousAttempt,
    PlaySessionResultsDto? PersonalBest,
    RecapDeltaDto VsPrevious,
    RecapDeltaDto VsAverage,
    bool IsNewPersonalBest
);

/// <summary>Signed deltas of the recap session against a baseline (positive = improved for score/combo).</summary>
public sealed record RecapDeltaDto(
    int ScoreDelta,
    float AccuracyDelta,
    int MaxComboDelta,
    int MissesDelta
);
