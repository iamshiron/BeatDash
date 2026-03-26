using Shiron.BeatDash.API.DTOs;

namespace Shiron.BeatDash.API.Services;

public interface IQueryService {
    Task<MapDto?> GetMapByIdAsync(long id);
    Task<MapDto?> GetMapByHashAsync(string hash);
    Task<MapDto?> GetMapByBSRKeyAsync(string bsrKey);
    Task<PaginatedResult<MapSummaryDto>> GetMapsAsync(MapFilterParams filter);
    Task<List<DifficultyDto>> GetMapDifficultiesAsync(long mapId);
    Task<MapDetailDto?> GetMapDetailAsync(long mapId);

    Task<PaginatedResult<PlaySessionSummaryDto>> GetPlaySessionsAsync(PlaySessionFilterParams filter);
    Task<PlaySessionDto?> GetPlaySessionByIdAsync(long id);
    Task<List<PlaySessionSummaryDto>> GetRecentSessionsAsync(int count = 10);
    Task<List<PlaySessionSummaryDto>> GetSessionsByMapIdAsync(long mapId, int count = 20);

    Task<PaginatedResult<LiveDataSnapshotDto>> GetLiveDataSnapshotsAsync(LiveDataSnapshotFilterParams filter);
    Task<List<LiveDataSnapshotDto>> GetSnapshotsBySessionIdAsync(long sessionId);
    Task<SessionPerformanceBreakdownDto?> GetSessionPerformanceBreakdownAsync(long sessionId);

    Task<AnalyticsOverviewDto> GetAnalyticsOverviewAsync();
    Task<List<PerformanceTrendDto>> GetPerformanceTrendAsync(PerformanceTrendFilterParams filter);
    Task<List<MapPlayStatsDto>> GetTopPlayedMapsAsync(int count = 10);
    Task<List<MapPlayStatsDto>> GetBestPerformingMapsAsync(int count = 10);
    Task<List<DifficultyStatsDto>> GetDifficultyStatsAsync();
    Task<List<MapTypeStatsDto>> GetMapTypeStatsAsync();
    Task<List<ScoreDistributionDto>> GetScoreDistributionAsync();
    Task<List<HourlyPlayStatsDto>> GetHourlyPlayStatsAsync();
    Task<List<DailyPlayStatsDto>> GetDailyPlayStatsAsync();
    Task<List<AccuracyDistributionDto>> GetAccuracyDistributionAsync();

    Task<DashboardDto> GetDashboardAsync();
    Task<SessionDetailDto?> GetSessionDetailAsync(long sessionId);

    Task<string?> GetMapCoverImageAsync(long mapId);
    Task<string?> GetMapCoverImageByHashAsync(string hash);
    Task<string?> GetMapCoverImageByBSRKeyAsync(string bsrKey);
}
