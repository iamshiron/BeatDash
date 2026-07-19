using System.Security.Claims;
using Shiron.BeatDash.API.Services.Health;

namespace Shiron.BeatDash.API.Endpoints;

/// <summary>
/// Owner-only fitness endpoints. Both no-op (204/404) when the caller hasn't enabled health
/// tracking, so the client surfaces stay hidden without special-casing.
/// </summary>
public static class HealthEndpoints {
    public static void MapHealthEndpoints(this IEndpointRouteBuilder endpoints) {
        endpoints.MapGet("/health/overview", async (
                ClaimsPrincipal principal, IHealthService health, CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(principal);
                if (userId is null) return Results.Unauthorized();
                var overview = await health.GetOverviewAsync(userId.Value, ct);
                return overview is null ? Results.NoContent() : Results.Ok(overview);
            })
            .WithName("GetHealthOverview")
            .WithDescription("The current user's fitness overview (calories, active time, trends, body context).")
            .WithTags("Health")
            .RequireAuthorization()
            .Produces<HealthOverviewDto>()
            .Produces(204)
            .Produces(401);

        endpoints.MapGet("/sessions/{sessionId:guid}/workout", async (
                Guid sessionId, ClaimsPrincipal principal, IHealthService health, CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(principal);
                if (userId is null) return Results.Unauthorized();
                var workout = await health.GetWorkoutAsync(userId.Value, sessionId, ct);
                return workout is null ? Results.NotFound() : Results.Ok(workout);
            })
            .WithName("GetSessionWorkout")
            .WithDescription("Per-play workout figures (calories, active time, intensity, movement, heart rate).")
            .WithTags("Health")
            .RequireAuthorization()
            .Produces<WorkoutDto>()
            .Produces(404)
            .Produces(401);
    }
}

/// <summary>A user's career/weekly/today fitness overview.</summary>
public sealed record HealthOverviewDto(
    double CareerKcal,
    double CareerActiveMinutes,
    double TotalSaberTravelKm,
    int TotalPlays,
    double TodayKcal,
    double TodayActiveMinutes,
    double WeekKcal,
    double WeekActiveMinutes,
    double AvgKcalPerPlay,
    double? Bmi,
    double? BmrKcalPerDay,
    double? LeanMassKg,
    int? RestingHeartRate,
    double? RecentAvgHeartRate,
    IList<HealthDayDto> ActivityDays,
    IList<HealthTrendWeekDto> Trend
);

/// <summary>Calories and active time burned on a single day (for the fitness heatmap).</summary>
public sealed record HealthDayDto(DateOnly Date, double Kcal, double ActiveMinutes);

/// <summary>Weekly fitness bucket for the trend chart.</summary>
public sealed record HealthTrendWeekDto(DateOnly WeekStart, double Kcal, double ActiveMinutes);

/// <summary>Per-play workout figures shown on the play detail page.</summary>
public sealed record WorkoutDto(
    double Kcal,
    double ActiveMinutes,
    double Intensity,
    double? Met,
    string Confidence,
    double LeftDistanceM,
    double RightDistanceM,
    double? AvgHeartRate,
    int? MaxHeartRate
);
