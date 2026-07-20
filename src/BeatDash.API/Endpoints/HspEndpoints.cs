using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shiron.BeatDash.API.Services.Health;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.API.Endpoints;

/// <summary>
/// Honami Sensor Proxy (HSP): the generalized, backend-agnostic ingestion surface a wearable /
/// companion app pushes sensor samples to. A session-authed endpoint mints the per-user push
/// token; a token-authenticated endpoint accepts a batch of typed samples. The Beat Saber client
/// is deliberately not involved.
/// </summary>
public static class HspEndpoints {
    private const int MaxBatch = 5000;
    private const string TokenHeader = "X-Honami-Token";
    private static readonly TimeSpan FutureSkew = TimeSpan.FromMinutes(5);

    public static void MapHspEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup("/hsp").WithTags("HSP");

        // Mint (or rotate) the caller's push token — returned in plaintext exactly once. Used to
        // build the QR a Honami client scans. Re-minting invalidates previously linked clients.
        group.MapPost("/token", async (ClaimsPrincipal principal, UserManager<User> userManager) => {
                var user = await userManager.GetUserAsync(principal);
                if (user is null) return Results.Unauthorized();

                var token = HealthIngestToken.Generate();
                user.HealthIngestTokenHash = HealthIngestToken.Hash(token);
                var result = await userManager.UpdateAsync(user);
                if (!result.Succeeded)
                    return Results.BadRequest(result.Errors.Select(e => e.Description).ToList());

                return Results.Ok(new HspTokenDto(token));
            })
            .WithName("GenerateHspToken")
            .WithDescription("Generate or rotate the Honami Sensor Proxy push token (plaintext shown once).")
            .RequireAuthorization()
            .Produces<HspTokenDto>()
            .Produces(401);

        // Token-authenticated push of a generalized sample batch from a Honami client.
        group.MapPost("/ingest", async (
                HttpContext http, HspIngestDto body, BeatDashDbContext db, CancellationToken ct) => {
                var token = ExtractToken(http.Request);
                if (token is null) return Results.Unauthorized();

                var hash = HealthIngestToken.Hash(token);
                var user = await db.Users.FirstOrDefaultAsync(u => u.HealthIngestTokenHash == hash, ct);
                if (user is null) return Results.Unauthorized();

                // Silently no-op when the user hasn't enabled tracking (token stays valid).
                if (!user.HealthTrackingEnabled)
                    return Results.Ok(new HspIngestResultDto(0));

                if (body.Samples is not { Count: > 0 })
                    return Results.BadRequest(new[] { "No samples provided." });
                if (body.Samples.Count > MaxBatch)
                    return Results.BadRequest(new[] { $"Too many samples (max {MaxBatch})." });

                var cutoff = DateTime.UtcNow + FutureSkew;
                var valid = body.Samples
                    .Where(s => !string.IsNullOrWhiteSpace(s.Metric) && HspMetrics.IsValid(s.Metric, s.Value))
                    .Select(s => new { s.Metric, s.Value, s.Unit, RecordedAt = ToUtc(s.RecordedAt), Source = s.Source ?? body.Source })
                    .Where(s => s.RecordedAt <= cutoff)
                    .ToList();
                if (valid.Count == 0)
                    return Results.Ok(new HspIngestResultDto(0));

                // Dedupe against existing rows in the batch's time span (unique on UserId+Metric+RecordedAt).
                var min = valid.Min(s => s.RecordedAt);
                var max = valid.Max(s => s.RecordedAt);
                var seen = (await db.SensorSamples
                        .Where(x => x.UserId == user.Id && x.RecordedAt >= min && x.RecordedAt <= max)
                        .Select(x => new { x.Metric, x.RecordedAt })
                        .ToListAsync(ct))
                    .Select(x => (x.Metric, x.RecordedAt))
                    .ToHashSet();

                var toInsert = valid
                    .Where(s => seen.Add((s.Metric, s.RecordedAt))) // also dedupes within the batch
                    .Select(s => new SensorSample {
                        UserId = user.Id,
                        Metric = s.Metric,
                        Value = s.Value,
                        Unit = s.Unit ?? HspMetrics.CanonicalUnit(s.Metric),
                        RecordedAt = s.RecordedAt,
                        Source = s.Source
                    })
                    .ToList();

                if (toInsert.Count > 0) {
                    db.SensorSamples.AddRange(toInsert);
                    await db.SaveChangesAsync(ct);
                }

                return Results.Ok(new HspIngestResultDto(toInsert.Count));
            })
            .WithName("HspIngest")
            .WithDescription("Push a batch of generalized sensor samples authenticated by the Honami push token.")
            .AllowAnonymous()
            .DisableAntiforgery()
            .Produces<HspIngestResultDto>()
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

/// <summary>The freshly generated push token; shown to the user once (encoded into the link QR).</summary>
public sealed record HspTokenDto(string Token);

/// <summary>A batch of generalized sensor samples.</summary>
public sealed record HspIngestDto(string? Source, IList<HspSampleDto> Samples);

/// <summary>One generalized sample: a metric, its value, and when it was measured.</summary>
public sealed record HspSampleDto(string Metric, double Value, string? Unit, DateTime RecordedAt, string? Source);

/// <summary>How many new samples were stored (after validation + dedupe).</summary>
public sealed record HspIngestResultDto(int Accepted);
