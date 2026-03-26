using Shiron.BeatDash.API.DTOs;
using Shiron.BeatDash.API.Services;

namespace Shiron.BeatDash.API.Endpoints;

public static class PlaySessionEndpoints {
    public static RouteGroupBuilder MapPlaySessionsApi(this RouteGroupBuilder group) {
        group.MapGet("/", GetPlaySessions)
            .WithName("GetPlaySessions")
            .WithDescription("Get a paginated list of play sessions with optional filtering");

        group.MapGet("/recent", GetRecentSessions)
            .WithName("GetRecentSessions")
            .WithDescription("Get the most recent play sessions");

        group.MapGet("/{id:long}", GetPlaySessionById)
            .WithName("GetPlaySessionById")
            .WithDescription("Get a specific play session by ID with full details");

        group.MapGet("/{id:long}/detail", GetSessionDetail)
            .WithName("GetSessionDetail")
            .WithDescription("Get comprehensive session detail with performance breakdown");

        group.MapGet("/{id:long}/snapshots", GetSessionSnapshots)
            .WithName("GetSessionSnapshots")
            .WithDescription("Get live data snapshots for a specific play session");

        group.MapGet("/{id:long}/performance", GetSessionPerformance)
            .WithName("GetSessionPerformance")
            .WithDescription("Get detailed performance breakdown for a play session");

        return group;
    }

    private static async Task<IResult> GetPlaySessions(
        IQueryService queryService,
        long? mapId = null,
        string? mapHash = null,
        string? endReason = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        bool? practiceMode = null,
        bool? multiplayer = null,
        string? difficulty = null,
        string? mapType = null,
        bool? fullCombo = null,
        double? minAccuracy = null,
        double? maxAccuracy = null,
        int page = 1,
        int pageSize = 20,
        string? sortBy = null,
        bool sortDescending = false) {
        var filter = new PlaySessionFilterParams {
            MapId = mapId,
            MapHash = mapHash,
            EndReason = endReason,
            FromDate = fromDate,
            ToDate = toDate,
            PracticeMode = practiceMode,
            Multiplayer = multiplayer,
            Difficulty = difficulty,
            MapType = mapType,
            FullCombo = fullCombo,
            MinAccuracy = minAccuracy,
            MaxAccuracy = maxAccuracy,
            Page = Math.Max(1, page),
            PageSize = Math.Clamp(pageSize, 1, 100),
            SortBy = sortBy,
            SortDescending = sortDescending
        };

        var result = await queryService.GetPlaySessionsAsync(filter);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetRecentSessions(
        IQueryService queryService,
        int count = 10) {
        var sessions = await queryService.GetRecentSessionsAsync(Math.Clamp(count, 1, 50));
        return Results.Ok(sessions);
    }

    private static async Task<IResult> GetPlaySessionById(long id, IQueryService queryService) {
        var session = await queryService.GetPlaySessionByIdAsync(id);
        return session == null ? Results.NotFound() : Results.Ok(session);
    }

    private static async Task<IResult> GetSessionDetail(long id, IQueryService queryService) {
        var detail = await queryService.GetSessionDetailAsync(id);
        return detail == null ? Results.NotFound() : Results.Ok(detail);
    }

    private static async Task<IResult> GetSessionSnapshots(
        long id,
        IQueryService queryService) {
        var snapshots = await queryService.GetSnapshotsBySessionIdAsync(id);
        return Results.Ok(snapshots);
    }

    private static async Task<IResult> GetSessionPerformance(
        long id,
        IQueryService queryService) {
        var performance = await queryService.GetSessionPerformanceBreakdownAsync(id);
        return performance == null ? Results.NotFound() : Results.Ok(performance);
    }
}
