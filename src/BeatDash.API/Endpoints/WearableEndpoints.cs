using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.API.Endpoints;

/// <summary>
/// Wearable/companion-app integration: an authed endpoint to mint a push token, and a
/// token-authenticated ingest endpoint the Beat Saber client is deliberately not involved in.
/// </summary>
public static class WearableEndpoints {
    private const int MaxBatch = 5000;
    private const string TokenHeader = "X-BeatDash-Health-Token";

    public static void MapWearableEndpoints(this IEndpointRouteBuilder endpoints) {
        // Mint (or rotate) the caller's ingest token — returns the plaintext once.
        endpoints.MapPost("/health/ingest-token", async (
                ClaimsPrincipal principal, UserManager<User> userManager) => {
                var user = await userManager.GetUserAsync(principal);
                if (user is null) return Results.Unauthorized();

                var token = HealthIngestToken.Generate();
                user.HealthIngestTokenHash = HealthIngestToken.Hash(token);
                var result = await userManager.UpdateAsync(user);
                if (!result.Succeeded)
                    return Results.BadRequest(result.Errors.Select(e => e.Description).ToList());

                return Results.Ok(new HealthIngestTokenDto(token));
            })
            .WithName("GenerateHealthIngestToken")
            .WithDescription("Generate or rotate the wearable ingest token (plaintext shown once).")
            .WithTags("Wearable")
            .RequireAuthorization()
            .Produces<HealthIngestTokenDto>()
            .Produces(401);

        // Token-authenticated push of heart-rate samples from a wearable/companion app.
        endpoints.MapPost("/health/ingest/heartrate", async (
                HttpContext http, HeartRateIngestDto body, BeatDashDbContext db, CancellationToken ct) => {
                var token = ExtractToken(http.Request);
                if (token is null) return Results.Unauthorized();

                var hash = HealthIngestToken.Hash(token);
                var user = await db.Users
                    .FirstOrDefaultAsync(u => u.HealthIngestTokenHash == hash, ct);
                if (user is null) return Results.Unauthorized();

                // Silently no-op when the user hasn't enabled tracking (token still valid).
                if (!user.HealthTrackingEnabled)
                    return Results.Ok(new HeartRateIngestResultDto(0));

                if (body.Samples is not { Count: > 0 })
                    return Results.BadRequest(new[] { "No samples provided." });
                if (body.Samples.Count > MaxBatch)
                    return Results.BadRequest(new[] { $"Too many samples (max {MaxBatch})." });

                var valid = body.Samples
                    .Where(s => s.Bpm is >= 20 and <= 240)
                    .Select(s => s with { RecordedAt = ToUtc(s.RecordedAt) })
                    .ToList();
                if (valid.Count == 0)
                    return Results.Ok(new HeartRateIngestResultDto(0));

                // Dedupe against existing rows in the batch's time span (unique on UserId+RecordedAt).
                var min = valid.Min(s => s.RecordedAt);
                var max = valid.Max(s => s.RecordedAt);
                var seen = (await db.HeartRateSamples
                        .Where(h => h.UserId == user.Id && h.RecordedAt >= min && h.RecordedAt <= max)
                        .Select(h => h.RecordedAt)
                        .ToListAsync(ct))
                    .ToHashSet();

                var toInsert = valid
                    .Where(s => seen.Add(s.RecordedAt)) // also dedupes within the batch
                    .Select(s => new HeartRateSample {
                        UserId = user.Id,
                        RecordedAt = s.RecordedAt,
                        Bpm = s.Bpm,
                        CaloriesKcal = s.CaloriesKcal,
                        Steps = s.Steps,
                        Source = s.Source
                    })
                    .ToList();

                if (toInsert.Count > 0) {
                    db.HeartRateSamples.AddRange(toInsert);
                    await db.SaveChangesAsync(ct);
                }

                return Results.Ok(new HeartRateIngestResultDto(toInsert.Count));
            })
            .WithName("IngestHeartRate")
            .WithDescription("Push a batch of heart-rate samples authenticated by the ingest token header.")
            .WithTags("Wearable")
            .AllowAnonymous()
            .DisableAntiforgery()
            .Produces<HeartRateIngestResultDto>()
            .Produces(400)
            .Produces(401);
    }

    private static string? ExtractToken(HttpRequest request) {
        var auth = request.Headers.Authorization.ToString();
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return auth["Bearer ".Length..].Trim();
        var header = request.Headers[TokenHeader].ToString();
        return string.IsNullOrWhiteSpace(header) ? null : header.Trim();
    }

    // Treat unspecified-kind timestamps as UTC (the API expects UTC ISO-8601).
    private static DateTime ToUtc(DateTime value) => value.Kind switch {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}

/// <summary>The freshly generated ingest token; shown to the user once.</summary>
public sealed record HealthIngestTokenDto(string Token);

/// <summary>A batch of wearable heart-rate samples.</summary>
public sealed record HeartRateIngestDto(IList<HeartRateSampleDto> Samples);

/// <summary>A single wearable sample.</summary>
public sealed record HeartRateSampleDto(
    DateTime RecordedAt,
    int Bpm,
    double? CaloriesKcal,
    int? Steps,
    string? Source
);

/// <summary>How many new samples were stored (after validation + dedupe).</summary>
public sealed record HeartRateIngestResultDto(int Accepted);
