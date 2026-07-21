namespace Shiron.BeatDash.API.Endpoints;

/// <summary>
/// Summary of one "session" in the player-facing sense — a sitting of many plays
/// with no long idle gap. (Distinct from a single <c>PlaySession</c>, which the UI
/// calls a "play".)
/// </summary>
public sealed record SessionSummaryDto(
    DateTime StartedAt,
    DateTime EndedAt,
    int PlayCount,
    // Plays that finished — the basis for accuracy/rank/PB stats. May be < PlayCount
    // when the sitting also contains failed/quit/incomplete attempts.
    int CompletedPlayCount,
    long TotalPlayTimeMs,
    float AvgAccuracy,
    int FullCombos,
    int UniqueMaps,
    int PersonalBests,
    double TotalSaberTravel,
    IList<RankCountDto> RankDistribution,
    IList<PlaySessionListItemDto> Plays,
    PlaySessionListItemDto? BestPlay,
    // Populated only when the user has health tracking on and a weight set.
    double? CaloriesKcal,
    double? ActiveMinutes
);

/// <summary>
/// At-a-glance totals across every one of the player's sittings. Derived from the play
/// timeline alone (no per-play hydration), so it stays cheap regardless of history size.
/// </summary>
public sealed record SittingsOverviewDto(
    int TotalSessions,
    int TotalPlays,
    // Summed wall-clock span of every sitting (first play's start → last play's end).
    long TotalActiveMs,
    double AvgPlaysPerSession,
    DateTime? LastPlayedAt
);
