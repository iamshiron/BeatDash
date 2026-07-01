using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shiron.BeatDash.API.DTOs;
using Shiron.BeatDash.API.Services;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.API.Endpoints;

public static class DeviceEndpoints {
    public static void MapDeviceEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup("/device").WithTags("Device");

        group.MapGet("/register", (IPinService pinService, ClaimsPrincipal user) => {
            var userId = IdentityUtils.GetUserID(user) ?? throw new InvalidOperationException("User not authenticated.");
            var pin = pinService.GeneratePin(userId, out var expires);
            return Results.Ok(new RegisterDeviceResponseDto(pin, expires));
        }).RequireAuthorization().Produces<RegisterDeviceResponseDto>();

        group.MapPost("/authenticate", async (
            IPinService pinService,
            ITokenService tokenService,
            BeatDashDbContext db,
            [FromBody] AuthenticateDeviceDto body,
            CancellationToken ct) => {
                var pin = body.Pin;
                if (!pinService.TryConsumePin(pin, out var userId)) {
                    return Results.Unauthorized();
                }

                var tokenPair = new TokenPairExpiryDto {
                    AccessToken = tokenService.GenerateAccessToken(userId, out var accessTokenExpires),
                    RefreshToken = tokenService.GenerateRefreshToken(out var refreshTokenExpires),
                    AccessExpiresAt = accessTokenExpires,
                    RefreshExpiresAt = refreshTokenExpires
                };

                var device = await db.Devices.SingleOrDefaultAsync(d => d.ClientId == body.ClientId && d.UserId == userId, ct);
                if (device == null) {
                    device = new Device {
                        ClientId = body.ClientId,
                        UserId = userId,
                        Name = "VR Headset"
                    };
                    db.Devices.Add(device);
                }

                await db.RefreshTokens.AddAsync(new RefreshToken {
                    DeviceId = device.Id,
                    Expires = refreshTokenExpires,
                    Token = tokenPair.RefreshToken
                }, ct);

                await db.SaveChangesAsync(ct);

                return Results.Ok(tokenPair);
            }).AllowAnonymous().Produces<TokenPairExpiryDto>();

        group.MapGet("/", async (ClaimsPrincipal user, BeatDashDbContext db, ISessionManager sessionManager, CancellationToken ct) => {
            var userId = IdentityUtils.GetUserID(user);
            if (!userId.HasValue) return Results.Unauthorized();

            var session = sessionManager.GetSession(userId.Value);
            var devices = await db.Devices
                .AsNoTracking()
                .Where(d => d.UserId == userId)
                .ToListAsync(ct);

            var res = devices.Select(device => new DeviceResponseDto(
                device.ClientId,
                device.Name,
                device.LastSeenAt,
                session is not null && session.ClientId == device.ClientId
                    ? new SessionDto(session.Id, device.Id, session.CreatedAt)
                    : null
            )).ToList();

            return Results.Ok(res);
        }).RequireAuthorization().Produces<IList<DeviceResponseDto>>();

        group.MapDelete("/{clientId:Guid}", async (ClaimsPrincipal user, BeatDashDbContext db, Guid clientId, CancellationToken ct) => {
            var userId = IdentityUtils.GetUserID(user);
            if (!userId.HasValue) return Results.Unauthorized();

            await db.Devices.Where(d => d.ClientId == clientId && d.UserId == userId)
                .ExecuteDeleteAsync(ct);

            return Results.NoContent();
        }).RequireAuthorization().Produces(204);

        group.MapPatch("/{clientId:Guid}", async (
            ClaimsPrincipal user,
            BeatDashDbContext db,
            Guid clientId,
            [FromBody] UpdateDeviceDto body,
            CancellationToken ct) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();

                var device = await db.Devices.SingleOrDefaultAsync(d => d.ClientId == clientId && d.UserId == userId, ct);
                if (device == null) return Results.NotFound();
                if (!string.IsNullOrEmpty(body.Name)) device.Name = body.Name;

                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            }).RequireAuthorization().Produces(204);
    }
}

public record RegisterDeviceResponseDto(string Pin, DateTime Expires);
public record AuthenticateDeviceDto(string Pin, Guid ClientId);
public record DeviceResponseDto(Guid ClientId, string Name, DateTime LastSeenAt, SessionDto? Session);
public record SessionDto(Guid SessionId, Guid DeviceId, DateTime OnlineSince);
public record UpdateDeviceDto(string? Name);
