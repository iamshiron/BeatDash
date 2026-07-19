using Microsoft.EntityFrameworkCore;
using Shiron.BeatDash.API.Services;
using Shiron.BeatDash.DB;

namespace Shiron.BeatDash.API.Endpoints;

public static class ProfileEndpoints {
    public static void MapProfileEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup("/profiles").WithTags("Profiles");

        group.MapGet("/{handle}", GetProfile)
            .WithName("GetPublicProfile")
            .WithDescription("Get a user's public profile by handle. Sections are included only when the owner has made them public.")
            .AllowAnonymous()
            .Produces<PublicProfileDto>()
            .Produces(404);
    }

    private static async Task<IResult> GetProfile(
        string handle,
        BeatDashDbContext db,
        IProfileStatsService profileStats,
        CancellationToken ct) {
        var normalized = HandleUtils.Normalize(handle);
        if (normalized is null) return Results.NotFound();

        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Handle == normalized, ct);
        if (user is null) return Results.NotFound();

        PublicProfileStatsDto? stats = null;
        IReadOnlyList<ActivityDayDto>? activity = null;
        PublicProfileHistoryDto? history = null;
        SkillProfileDto? skill = null;

        // Stats, activity and history all derive from the same aggregate — compute it
        // once when any of those sections is public, then expose only what's permitted.
        if (user.ProfileStatsPublic || user.ProfileActivityPublic || user.ProfileHistoryPublic) {
            var full = await profileStats.GetStatsAsync(user.Id, ct);

            if (user.ProfileStatsPublic) {
                stats = new PublicProfileStatsDto(
                    full.TotalPlays,
                    full.TotalPlayTimeMs,
                    full.AverageAccuracy,
                    full.FullCombos,
                    full.UniqueMaps,
                    full.RankDistribution,
                    full.MostPlayedMaps);
            }

            if (user.ProfileActivityPublic) activity = full.Activity.ToList();

            if (user.ProfileHistoryPublic) {
                history = new PublicProfileHistoryDto(full.RecentSessions, full.TopScores);
            }
        }

        if (user.ProfileSkillPublic) {
            skill = await profileStats.GetSkillAsync(user.Id, ct);
        }

        IReadOnlyList<PublicPlaylistDto>? playlists = null;
        if (user.ProfileListsPublic) {
            playlists = await db.MapLists
                .AsNoTracking()
                .Where(l => l.UserId == user.Id)
                .OrderByDescending(l => l.UpdatedAt)
                .Take(12)
                .Select(l => new PublicPlaylistDto(
                    l.Id,
                    l.Name,
                    l.Items.Count,
                    l.Items
                        .OrderBy(i => i.Position)
                        .Where(i => i.Beatmap.CoverImageKey != null)
                        .Select(i => i.BeatmapId)
                        .Take(4)
                        .ToList()))
                .ToListAsync(ct);
        }

        IReadOnlyList<PublicLikedMapDto>? likedMaps = null;
        if (user.ProfileLikedPublic) {
            likedMaps = await db.MapLikes
                .AsNoTracking()
                .Where(l => l.UserId == user.Id)
                .OrderByDescending(l => l.CreatedAt)
                .Take(24)
                .Select(l => new PublicLikedMapDto(
                    l.BeatmapId,
                    l.Beatmap.SongName,
                    l.Beatmap.SongAuthor,
                    l.Beatmap.Mapper))
                .ToListAsync(ct);
        }

        return Results.Ok(new PublicProfileDto(
            user.Handle!,
            user.DisplayName,
            user.AvatarKey is null ? null : $"/api/users/{user.Id}/avatar",
            user.BannerKey is null ? null : $"/api/users/{user.Id}/banner",
            stats,
            activity,
            skill,
            history,
            playlists,
            likedMaps));
    }
}

/// <summary>
/// A user's public profile. Each section is null when the owner has kept it private, so
/// private data never leaves the API. The identity header (handle + display name) is always present.
/// </summary>
public sealed record PublicProfileDto(
    string Handle,
    string DisplayName,
    string? AvatarUrl,
    string? BannerUrl,
    PublicProfileStatsDto? Stats,
    IReadOnlyList<ActivityDayDto>? Activity,
    SkillProfileDto? Skill,
    PublicProfileHistoryDto? History,
    IReadOnlyList<PublicPlaylistDto>? Playlists,
    IReadOnlyList<PublicLikedMapDto>? LikedMaps
);

/// <summary>A user's playlist as shown on their public profile (display-only).</summary>
public sealed record PublicPlaylistDto(
    Guid Id,
    string Name,
    int MapCount,
    IList<Guid> CoverMapIds
);

/// <summary>A liked map shown on a public profile.</summary>
public sealed record PublicLikedMapDto(
    Guid BeatmapId,
    string SongName,
    string SongAuthor,
    string Mapper
);

/// <summary>Headline stats shown at a glance on a public profile.</summary>
public sealed record PublicProfileStatsDto(
    int TotalPlays,
    long TotalPlayTimeMs,
    float AverageAccuracy,
    int FullCombos,
    int UniqueMaps,
    IList<RankCountDto> RankDistribution,
    IList<MostPlayedMapDto> MostPlayedMaps
);

/// <summary>Recent and best-accuracy plays shown on a public profile.</summary>
public sealed record PublicProfileHistoryDto(
    IList<PlaySessionListItemDto> RecentSessions,
    IList<PlaySessionListItemDto> TopScores
);
