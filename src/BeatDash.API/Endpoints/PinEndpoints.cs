using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shiron.BeatDash.API.DTOs;
using Shiron.BeatDash.API.Services;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.API.Endpoints;

public static class PinEndpoints {
    public static void MapPinEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup("/pin").WithTags("Pin");

        group.MapGet("/create", (IPinService pinService, ClaimsPrincipal user) => {
            var userId = IdentityUtils.GetUserID(user) ?? throw new InvalidOperationException("User not authenticated.");
            var pin = pinService.GeneratePin(userId, out var expires);
            return Results.Ok(new PinResponseDto(pin, expires));
        }).RequireAuthorization().Produces<PinResponseDto>();

        group.MapPost("/authenticate", async (
            IPinService pinService,
            ITokenService tokenService,
            BeatDashDbContext db,
            [FromBody] PinAuthenticateDto body,
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
                        Name = "VR Headset",
                    };
                    db.Devices.Add(device);
                }

                await db.RefreshTokens.AddAsync(new RefreshToken {
                    DeviceId = device.Id,
                    Expires = refreshTokenExpires,
                    Token = tokenPair.RefreshToken,
                }, ct);

                await db.SaveChangesAsync(ct);

                return Results.Ok(tokenPair);
            }).AllowAnonymous().Produces<TokenPairExpiryDto>();
    }
}

public record PinResponseDto(string Pin, DateTime Expires);
public record PinAuthenticateDto(string Pin, Guid ClientId);
