using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Shiron.BeatDash.API.Services.Health;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.API.Endpoints;

/// <summary>
/// Honami Sensor Proxy (HSP): the generalized, backend-agnostic ingestion surface a wearable /
/// companion app pushes sensor samples to. Client management (link / list / unlink) is guarded by
/// the user's normal session; only <c>/hsp/ingest</c> accepts the opaque per-client push token,
/// which is validated by hash here and nowhere else — so it can never reach any other endpoint.
/// Multiple clients can be linked and push concurrently.
/// </summary>
public static class HspEndpoints {
    private const int MaxBatch = 5000;
    private const int MaxNameLength = 64;
    private const string TokenHeader = "X-Honami-Token";
    private const string DefaultName = "Honami client";
    private static readonly TimeSpan FutureSkew = TimeSpan.FromMinutes(5);

    public static void MapHspEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup("/hsp").WithTags("HSP");

        // Link a new client — mints a scoped token (plaintext returned once, for the QR).
        group.MapPost("/clients", async (
                HspLinkRequestDto? body, ClaimsPrincipal principal, BeatDashDbContext db, CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(principal);
                if (userId is null) return Results.Unauthorized();

                var name = string.IsNullOrWhiteSpace(body?.Name) ? DefaultName : body.Name.Trim();
                if (name.Length > MaxNameLength) name = name[..MaxNameLength];

                var token = HealthIngestToken.Generate();
                var client = new HealthProxyClient {
                    UserId = userId.Value,
                    Name = name,
                    TokenHash = HealthIngestToken.Hash(token)
                };
                db.HealthProxyClients.Add(client);
                await db.SaveChangesAsync(ct);

                return Results.Ok(new HspClientTokenDto(client.Id, client.Name, token));
            })
            .WithName("LinkHspClient")
            .WithDescription("Link a new Honami Sensor Proxy client and mint its scoped push token (shown once).")
            .RequireAuthorization()
            .Produces<HspClientTokenDto>()
            .Produces(401);

        // List the caller's linked clients (for the Devices list).
        group.MapGet("/clients", async (ClaimsPrincipal principal, BeatDashDbContext db, CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(principal);
                if (userId is null) return Results.Unauthorized();

                var clients = await db.HealthProxyClients
                    .AsNoTracking()
                    .Where(c => c.UserId == userId)
                    .OrderBy(c => c.CreatedAt)
                    .Select(c => new HspClientDto(
                        c.Id, c.Name, c.CreatedAt, c.LastSeenAt,
                        // Most recent heart-rate this client pushed — a quick live readout.
                        db.SensorSamples
                            .Where(s => s.ClientId == c.Id && s.Metric == HspMetrics.HeartRate)
                            .OrderByDescending(s => s.RecordedAt)
                            .Select(s => (double?) s.Value)
                            .FirstOrDefault()))
                    .ToListAsync(ct);

                return Results.Ok(clients);
            })
            .WithName("ListHspClients")
            .WithDescription("List the caller's linked Honami Sensor Proxy clients.")
            .RequireAuthorization()
            .Produces<IList<HspClientDto>>()
            .Produces(401);

        // Unlink (revoke) a client.
        group.MapDelete("/clients/{id:guid}", async (
                Guid id, ClaimsPrincipal principal, BeatDashDbContext db, CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(principal);
                if (userId is null) return Results.Unauthorized();

                await db.HealthProxyClients
                    .Where(c => c.Id == id && c.UserId == userId)
                    .ExecuteDeleteAsync(ct);
                return Results.NoContent();
            })
            .WithName("UnlinkHspClient")
            .WithDescription("Unlink a Honami Sensor Proxy client, revoking its token.")
            .RequireAuthorization()
            .Produces(204)
            .Produces(401);

        // Token-authenticated push of a generalized sample batch from a linked client.
        group.MapPost("/ingest", async (
                HttpContext http, HspIngestDto body, BeatDashDbContext db,
                ILoggerFactory loggerFactory, CancellationToken ct) => {
                var logger = loggerFactory.CreateLogger("Hsp.Ingest");
                var token = ExtractToken(http.Request);
                if (token is null) return Results.Unauthorized();

                var hash = HealthIngestToken.Hash(token);
                var client = await db.HealthProxyClients.FirstOrDefaultAsync(c => c.TokenHash == hash, ct);
                if (client is null) {
                    logger.LogWarning("HSP ingest rejected: no client matches the supplied token.");
                    return Results.Unauthorized();
                }

                // The client authenticated — mark it connected now, regardless of what the batch
                // contains (an empty/all-invalid push still proves the client reached us).
                client.LastSeenAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                var received = body.Samples?.Count ?? 0;
                var dump = body.Samples is { Count: > 0 }
                    ? string.Join(", ", body.Samples.Select(s => $"{s.Metric}={s.Value}{s.Unit}@{s.RecordedAt:o}"))
                    : "(none)";
                logger.LogInformation(
                    "HSP ingest from '{Client}' (source={Source}): {Count} sample(s): {Samples}",
                    client.Name, body.Source ?? "(none)", received, dump);

                // Silently no-op when the owner hasn't enabled tracking (token stays valid).
                var trackingEnabled = await db.Users
                    .Where(u => u.Id == client.UserId)
                    .Select(u => u.HealthTrackingEnabled)
                    .FirstOrDefaultAsync(ct);
                if (!trackingEnabled)
                    return Results.Ok(new HspIngestResultDto(0, received, 0));

                if (body.Samples is not { Count: > 0 })
                    return Results.BadRequest(new[] { "No samples provided." });
                if (body.Samples.Count > MaxBatch)
                    return Results.BadRequest(new[] { $"Too many samples (max {MaxBatch})." });

                var cutoff = DateTime.UtcNow + FutureSkew;
                var valid = body.Samples
                    .Where(s => !string.IsNullOrWhiteSpace(s.Metric) && HspMetrics.IsValid(s.Metric, s.Value))
                    .Select(s => new { s.Metric, s.Value, s.Unit, RecordedAt = ToUtc(s.RecordedAt), Source = s.Source ?? body.Source ?? client.Name })
                    .Where(s => s.RecordedAt <= cutoff)
                    .ToList();
                var rejected = received - valid.Count;
                if (valid.Count == 0)
                    return Results.Ok(new HspIngestResultDto(0, received, rejected));

                // Dedupe against existing rows in the batch's time span (unique on UserId+Metric+RecordedAt).
                var min = valid.Min(s => s.RecordedAt);
                var max = valid.Max(s => s.RecordedAt);
                var seen = (await db.SensorSamples
                        .Where(x => x.UserId == client.UserId && x.RecordedAt >= min && x.RecordedAt <= max)
                        .Select(x => new { x.Metric, x.RecordedAt })
                        .ToListAsync(ct))
                    .Select(x => (x.Metric, x.RecordedAt))
                    .ToHashSet();

                var toInsert = valid
                    .Where(s => seen.Add((s.Metric, s.RecordedAt))) // also dedupes within the batch
                    .Select(s => new SensorSample {
                        UserId = client.UserId,
                        ClientId = client.Id,
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

                logger.LogInformation(
                    "HSP ingest from '{Client}': accepted={Accepted}, received={Received}, rejected={Rejected}.",
                    client.Name, toInsert.Count, received, rejected);
                return Results.Ok(new HspIngestResultDto(toInsert.Count, received, rejected));
            })
            .WithName("HspIngest")
            .WithDescription("Push a batch of generalized sensor samples authenticated by a Honami client token.")
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

/// <summary>Request to link a new client; an optional label for the Devices list.</summary>
public sealed record HspLinkRequestDto(string? Name);

/// <summary>A freshly linked client with its plaintext token — shown once, encoded into the link QR.</summary>
public sealed record HspClientTokenDto(Guid Id, string Name, string Token);

/// <summary>A linked client as shown in the Devices list, with its most recent heart rate.</summary>
public sealed record HspClientDto(
    Guid Id, string Name, DateTime CreatedAt, DateTime? LastSeenAt, double? LastHeartRate);

/// <summary>A batch of generalized sensor samples.</summary>
public sealed record HspIngestDto(string? Source, IList<HspSampleDto> Samples);

/// <summary>One generalized sample: a metric, its value, and when it was measured.</summary>
public sealed record HspSampleDto(string Metric, double Value, string? Unit, DateTime RecordedAt, string? Source);

/// <summary>
/// Ingest outcome: <paramref name="Accepted"/> newly stored (after validation + dedupe),
/// out of <paramref name="Received"/> sent, with <paramref name="Rejected"/> dropped for failing
/// validation (unknown metric, out-of-range value, or a timestamp too far in the future).
/// </summary>
public sealed record HspIngestResultDto(int Accepted, int Received, int Rejected);
