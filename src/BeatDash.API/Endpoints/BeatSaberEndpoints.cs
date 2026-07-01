using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Shiron.BeatDash.API.Services;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.API.Endpoints;

public static class BeatSaberEndpoints {
    private const int MaxPin = 1000000;

    public static void MapBeatSaberEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup("/beatsaber").WithTags("BeatSaber");

        group.MapGet("/auth-token", async (ClaimsPrincipal user, BeatDashDbContext db) => {
            var userId = IdentityUtils.GetUserID(user) ?? throw new InvalidOperationException("User not authenticated.");

            var existingToken = db.AuthTokenSessions.FirstOrDefault(s => s.UserId == userId && !s.Revoked);
            if (existingToken != null) {
                return new AuthTokenDto(existingToken.Token, existingToken.Expires);
            }

            var token = RandomNumberGenerator.GetInt32(0, MaxPin).ToString("D6");
            var expires = DateTime.UtcNow.AddMinutes(10);
            db.AuthTokenSessions.Add(new AuthTokenSession {
                Token = token,
                UserId = userId,
                Expires = DateTime.UtcNow.AddMinutes(10)
            });

            await db.SaveChangesAsync();
            return new AuthTokenDto(token, expires);
        })
            .RequireAuthorization(new AuthorizationPolicyBuilder(IdentityConstants.ApplicationScheme).RequireAuthenticatedUser().Build())
            .Produces<AuthTokenDto>();

        group.MapPost("/auth-client", async (BeatDashDbContext db, ITokenService tokenService, RequestAuthClientDto dto) => {
            var session = db.AuthTokenSessions.FirstOrDefault(s => s.Token == dto.Token);
            if (session == null || session.Expires < DateTime.UtcNow || session.Revoked) {
                return Results.Unauthorized();
            }
            db.AuthTokenSessions.Remove(session);

            var accessToken = tokenService.GenerateAccessToken(session.UserId, out _);
            var refreshToken = tokenService.GenerateRefreshToken(out var refreshExpire);
            db.RefreshTokens.Add(new RefreshToken {
                UserId = session.UserId,
                Token = refreshToken,
                Expires = refreshExpire,
                Revoked = false
            });

            await db.SaveChangesAsync();
            return Results.Ok(new AuthTokenSessionDto(accessToken, refreshToken));
        }).AllowAnonymous().Produces<AuthTokenSessionDto>();

        group.MapGet("/me", (ClaimsPrincipal user, BeatDashDbContext db) => {
            var userId = IdentityUtils.GetUserID(user) ?? throw new InvalidOperationException("User not authenticated.");
            var userData = db.Users.FirstOrDefault(u => u.Id == userId) ?? throw new InvalidOperationException("User not authenticated.");

            return Results.Ok(
                new MeResponseDto(userData.DisplayName, userData.UserName!, userData.Email!)
            );
        })
            .RequireAuthorization(new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser().Build())
            .Produces<MeResponseDto>();
    }

    public record AuthTokenDto(string Token, DateTime Expires);
    public record RequestAuthClientDto(string Token);
    public record AuthTokenSessionDto(string AccessToken, string RefreshToken);
    public record MeResponseDto(string DisplayName, string UserName, string Email);
}
