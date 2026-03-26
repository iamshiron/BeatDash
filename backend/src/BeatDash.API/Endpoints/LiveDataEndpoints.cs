using Shiron.BeatDash.API.DTOs;
using Shiron.BeatDash.API.Services;

namespace Shiron.BeatDash.API.Endpoints;

public static class LiveDataEndpoints {
    public static RouteGroupBuilder MapLiveDataApi(this RouteGroupBuilder group) {
        group.MapGet("/", GetLiveDataSnapshots)
            .WithName("GetLiveDataSnapshots")
            .WithDescription("Get a paginated list of live data snapshots with optional filtering");

        return group;
    }

    private static async Task<IResult> GetLiveDataSnapshots(
        IQueryService queryService,
        long? playSessionId = null,
        DateTimeOffset? fromTimestamp = null,
        DateTimeOffset? toTimestamp = null,
        int? eventTrigger = null,
        bool? fullCombo = null,
        double? minAccuracy = null,
        double? maxAccuracy = null,
        int page = 1,
        int pageSize = 50,
        string? sortBy = null,
        bool sortDescending = false) {
        var filter = new LiveDataSnapshotFilterParams {
            PlaySessionId = playSessionId,
            FromTimestamp = fromTimestamp,
            ToTimestamp = toTimestamp,
            EventTrigger = eventTrigger,
            FullCombo = fullCombo,
            MinAccuracy = minAccuracy,
            MaxAccuracy = maxAccuracy,
            Page = Math.Max(1, page),
            PageSize = Math.Clamp(pageSize, 1, 500),
            SortBy = sortBy,
            SortDescending = sortDescending
        };

        var result = await queryService.GetLiveDataSnapshotsAsync(filter);
        return Results.Ok(result);
    }
}
