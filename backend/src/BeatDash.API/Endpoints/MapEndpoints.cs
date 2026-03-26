using Shiron.BeatDash.API.DTOs;
using Shiron.BeatDash.API.Services;

namespace Shiron.BeatDash.API.Endpoints;

public static class MapEndpoints {
    public static RouteGroupBuilder MapMapsApi(this RouteGroupBuilder group) {
        group.MapGet("/", GetMaps)
            .WithName("GetMaps")
            .WithDescription("Get a paginated list of maps with optional filtering");

        group.MapGet("/{id:long}", GetMapById)
            .WithName("GetMapById")
            .WithDescription("Get a specific map by ID");

        group.MapGet("/{id:long}/detail", GetMapDetail)
            .WithName("GetMapDetail")
            .WithDescription("Get detailed map information including stats and play history");

        group.MapGet("/{id:long}/difficulties", GetMapDifficulties)
            .WithName("GetMapDifficulties")
            .WithDescription("Get all difficulties for a specific map");

        group.MapGet("/{id:long}/sessions", GetMapSessions)
            .WithName("GetMapSessions")
            .WithDescription("Get play sessions for a specific map");

        group.MapGet("/{id:long}/cover", GetMapCover)
            .WithName("GetMapCover")
            .WithDescription("Get the cover image for a map by ID");

        group.MapGet("/hash/{hash}", GetMapByHash)
            .WithName("GetMapByHash")
            .WithDescription("Get a specific map by hash");

        group.MapGet("/hash/{hash}/cover", GetMapCoverByHash)
            .WithName("GetMapCoverByHash")
            .WithDescription("Get the cover image by map hash");

        group.MapGet("/bsr/{bsrKey}", GetMapByBSRKey)
            .WithName("GetMapByBSRKey")
            .WithDescription("Get a specific map by BeatSaver key");

        group.MapGet("/bsr/{bsrKey}/cover", GetMapCoverByBSRKey)
            .WithName("GetMapCoverByBSRKey")
            .WithDescription("Get the cover image by BeatSaver key");

        return group;
    }

    private static async Task<IResult> GetMaps(
        IQueryService queryService,
        string? search = null,
        string? songAuthor = null,
        string? mapper = null,
        int? minBpm = null,
        int? maxBpm = null,
        int? minDuration = null,
        int? maxDuration = null,
        string? bsrKey = null,
        bool? hasBeenPlayed = null,
        int page = 1,
        int pageSize = 20,
        string? sortBy = null,
        bool sortDescending = false) {
        var filter = new MapFilterParams {
            Search = search,
            SongAuthor = songAuthor,
            Mapper = mapper,
            MinBPM = minBpm,
            MaxBPM = maxBpm,
            MinDuration = minDuration,
            MaxDuration = maxDuration,
            BSRKey = bsrKey,
            HasBeenPlayed = hasBeenPlayed,
            Page = Math.Max(1, page),
            PageSize = Math.Clamp(pageSize, 1, 100),
            SortBy = sortBy,
            SortDescending = sortDescending
        };

        var result = await queryService.GetMapsAsync(filter);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetMapById(long id, IQueryService queryService) {
        var map = await queryService.GetMapByIdAsync(id);
        return map == null ? Results.NotFound() : Results.Ok(map);
    }

    private static async Task<IResult> GetMapDetail(long id, IQueryService queryService) {
        var detail = await queryService.GetMapDetailAsync(id);
        return detail == null ? Results.NotFound() : Results.Ok(detail);
    }

    private static async Task<IResult> GetMapDifficulties(long id, IQueryService queryService) {
        var difficulties = await queryService.GetMapDifficultiesAsync(id);
        return Results.Ok(difficulties);
    }

    private static async Task<IResult> GetMapSessions(
        long id,
        IQueryService queryService,
        int count = 20) {
        var sessions = await queryService.GetSessionsByMapIdAsync(id, Math.Clamp(count, 1, 100));
        return Results.Ok(sessions);
    }

    private static async Task<IResult> GetMapCover(long id, IQueryService queryService) {
        var coverImage = await queryService.GetMapCoverImageAsync(id);
        return GetCoverImageResult(coverImage);
    }

    private static async Task<IResult> GetMapByHash(string hash, IQueryService queryService) {
        var map = await queryService.GetMapByHashAsync(hash);
        return map == null ? Results.NotFound() : Results.Ok(map);
    }

    private static async Task<IResult> GetMapCoverByHash(string hash, IQueryService queryService) {
        var coverImage = await queryService.GetMapCoverImageByHashAsync(hash);
        return GetCoverImageResult(coverImage);
    }

    private static async Task<IResult> GetMapByBSRKey(string bsrKey, IQueryService queryService) {
        var map = await queryService.GetMapByBSRKeyAsync(bsrKey);
        return map == null ? Results.NotFound() : Results.Ok(map);
    }

    private static async Task<IResult> GetMapCoverByBSRKey(string bsrKey, IQueryService queryService) {
        var coverImage = await queryService.GetMapCoverImageByBSRKeyAsync(bsrKey);
        return GetCoverImageResult(coverImage);
    }

    private static IResult GetCoverImageResult(string? coverImage) {
        if (string.IsNullOrEmpty(coverImage)) {
            return Results.NotFound("Cover image not available");
        }

        var base64Match = System.Text.RegularExpressions.Regex.Match(coverImage, @"^data:image/(\w+);base64,(.+)$");
        if (!base64Match.Success) {
            return Results.BadRequest("Invalid cover image format");
        }

        var imageFormat = base64Match.Groups[1].Value;
        var base64Data = base64Match.Groups[2].Value;

        try {
            var imageBytes = Convert.FromBase64String(base64Data);
            var contentType = imageFormat.ToLower() switch {
                "webp" => "image/webp",
                "png" => "image/png",
                "jpg" or "jpeg" => "image/jpeg",
                "gif" => "image/gif",
                _ => "application/octet-stream"
            };

            return Results.File(imageBytes, contentType);
        } catch (FormatException) {
            return Results.BadRequest("Invalid base64 data");
        }
    }
}
