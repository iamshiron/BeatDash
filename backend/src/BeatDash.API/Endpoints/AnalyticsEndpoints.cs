using Shiron.BeatDash.API.DTOs;
using Shiron.BeatDash.API.Services;

namespace Shiron.BeatDash.API.Endpoints;

public static class AnalyticsEndpoints {
    public static RouteGroupBuilder MapAnalyticsApi(this RouteGroupBuilder group) {
        group.MapGet("/dashboard", GetDashboard)
            .WithName("GetDashboard")
            .WithDescription("Get comprehensive dashboard data in a single request");

        group.MapGet("/overview", GetOverview)
            .WithName("GetAnalyticsOverview")
            .WithDescription("Get overall analytics and statistics summary");

        group.MapGet("/trend", GetPerformanceTrend)
            .WithName("GetPerformanceTrend")
            .WithDescription("Get performance trends over time");

        group.MapGet("/maps/top-played", GetTopPlayedMaps)
            .WithName("GetTopPlayedMaps")
            .WithDescription("Get the most played maps");

        group.MapGet("/maps/best-performing", GetBestPerformingMaps)
            .WithName("GetBestPerformingMaps")
            .WithDescription("Get maps with the best average performance");

        group.MapGet("/difficulty-stats", GetDifficultyStats)
            .WithName("GetDifficultyStats")
            .WithDescription("Get statistics grouped by difficulty level");

        group.MapGet("/map-type-stats", GetMapTypeStats)
            .WithName("GetMapTypeStats")
            .WithDescription("Get statistics grouped by map type");

        group.MapGet("/score-distribution", GetScoreDistribution)
            .WithName("GetScoreDistribution")
            .WithDescription("Get distribution of score ranks (SSS, S, A, etc.)");

        group.MapGet("/hourly-stats", GetHourlyPlayStats)
            .WithName("GetHourlyPlayStats")
            .WithDescription("Get play statistics grouped by hour of day");

        group.MapGet("/daily-stats", GetDailyPlayStats)
            .WithName("GetDailyPlayStats")
            .WithDescription("Get play statistics grouped by day of week");

        group.MapGet("/accuracy-distribution", GetAccuracyDistribution)
            .WithName("GetAccuracyDistribution")
            .WithDescription("Get distribution of accuracy percentages");

        return group;
    }

    private static async Task<IResult> GetDashboard(IQueryService queryService) {
        var dashboard = await queryService.GetDashboardAsync();
        return Results.Ok(dashboard);
    }

    private static async Task<IResult> GetOverview(IQueryService queryService) {
        var overview = await queryService.GetAnalyticsOverviewAsync();
        return Results.Ok(overview);
    }

    private static async Task<IResult> GetPerformanceTrend(
        IQueryService queryService,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        string groupBy = "day") {
        var filter = new PerformanceTrendFilterParams {
            FromDate = fromDate,
            ToDate = toDate,
            GroupBy = groupBy
        };

        var trend = await queryService.GetPerformanceTrendAsync(filter);
        return Results.Ok(trend);
    }

    private static async Task<IResult> GetTopPlayedMaps(
        IQueryService queryService,
        int count = 10) {
        var maps = await queryService.GetTopPlayedMapsAsync(Math.Clamp(count, 1, 50));
        return Results.Ok(maps);
    }

    private static async Task<IResult> GetBestPerformingMaps(
        IQueryService queryService,
        int count = 10) {
        var maps = await queryService.GetBestPerformingMapsAsync(Math.Clamp(count, 1, 50));
        return Results.Ok(maps);
    }

    private static async Task<IResult> GetDifficultyStats(IQueryService queryService) {
        var stats = await queryService.GetDifficultyStatsAsync();
        return Results.Ok(stats);
    }

    private static async Task<IResult> GetMapTypeStats(IQueryService queryService) {
        var stats = await queryService.GetMapTypeStatsAsync();
        return Results.Ok(stats);
    }

    private static async Task<IResult> GetScoreDistribution(IQueryService queryService) {
        var distribution = await queryService.GetScoreDistributionAsync();
        return Results.Ok(distribution);
    }

    private static async Task<IResult> GetHourlyPlayStats(IQueryService queryService) {
        var stats = await queryService.GetHourlyPlayStatsAsync();
        return Results.Ok(stats);
    }

    private static async Task<IResult> GetDailyPlayStats(IQueryService queryService) {
        var stats = await queryService.GetDailyPlayStatsAsync();
        return Results.Ok(stats);
    }

    private static async Task<IResult> GetAccuracyDistribution(IQueryService queryService) {
        var distribution = await queryService.GetAccuracyDistributionAsync();
        return Results.Ok(distribution);
    }
}
