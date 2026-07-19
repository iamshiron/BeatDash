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
