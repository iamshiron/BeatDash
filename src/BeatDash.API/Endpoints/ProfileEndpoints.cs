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

        return Results.Ok(new PublicProfileDto(
            user.Handle!,
            user.DisplayName,
            stats,
            activity,
            skill,
            history));
    }
}

/// <summary>
/// A user's public profile. Each section is null when the owner has kept it private, so
/// private data never leaves the API. The identity header (handle + display name) is always present.
/// </summary>
public sealed record PublicProfileDto(
    string Handle,
    string DisplayName,
    PublicProfileStatsDto? Stats,
    IReadOnlyList<ActivityDayDto>? Activity,
    SkillProfileDto? Skill,
    PublicProfileHistoryDto? History
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
