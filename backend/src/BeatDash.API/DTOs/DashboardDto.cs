namespace Shiron.BeatDash.API.DTOs;

public record DashboardDto {
    public AnalyticsOverviewDto Overview { get; init; } = null!;
    public List<PlaySessionSummaryDto> RecentSessions { get; init; } = [];
    public List<MapPlayStatsDto> TopPlayedMaps { get; init; } = [];
    public List<PerformanceTrendDto> RecentTrend { get; init; } = [];
    public List<ScoreDistributionDto> ScoreDistribution { get; init; } = [];
    public List<DailyPlayStatsDto> DailyStats { get; init; } = [];
}

public record MapDetailDto {
    public MapDto Map { get; init; } = null!;
    public MapPlayStatsDto? Stats { get; init; }
    public List<PlaySessionSummaryDto> RecentSessions { get; init; } = [];
    public List<DifficultyStatsDto> DifficultyStats { get; init; } = [];
    public PerformanceTrendDto? BestPerformance { get; init; }
}

public record SessionDetailDto {
    public PlaySessionDto Session { get; init; } = null!;
    public SessionPerformanceBreakdownDto Performance { get; init; } = null!;
    public int SnapshotCount { get; init; }
    public int MissCount { get; init; }
    public int MaxCombo { get; init; }
    public double AverageHealth { get; init; }
}
