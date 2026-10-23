using System.Security.Claims;
using Shiron.BeatDash.API.Services;
using Xunit;

namespace Shiron.BeatDash.API.Tests;

public sealed class TokenServiceTests {
    private const string Secret = "test-secret-that-is-long-enough-for-hmac-sha256";
    private const string Issuer = "beatdash-tests";
    private const string Audience = "beatdash-client";

    [Fact]
    public void GenerateAccessToken_ValidUser_CanBeReadAfterExpiry() {
        var service = new TokenService(Secret, Issuer, Audience);
        var userId = Guid.NewGuid();

        var accessToken = service.GenerateAccessToken(userId, out var expires);
        var principal = service.FromExpiredToken(accessToken);

        Assert.NotNull(principal);
        Assert.Equal(userId.ToString(), principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.InRange(expires, DateTime.UtcNow.AddMinutes(14), DateTime.UtcNow.AddMinutes(16));
    }

    [Fact]
    public void FromExpiredToken_InvalidOrWronglySignedToken_ReturnsNull() {
        var service = new TokenService(Secret, Issuer, Audience);
        var differentService = new TokenService("another-test-secret-that-is-long-enough-for-hmac", Issuer, Audience);
        var otherToken = differentService.GenerateAccessToken(Guid.NewGuid(), out _);

        Assert.Null(service.FromExpiredToken("not-a-token"));
        Assert.Null(service.FromExpiredToken(otherToken));
    }

    [Fact]
    public void GenerateRefreshToken_CalledTwice_ReturnsUniqueUrlSafeTokens() {
        var service = new TokenService(Secret, Issuer, Audience);

        var first = service.GenerateRefreshToken(out var firstExpiry);
        var second = service.GenerateRefreshToken(out var secondExpiry);

        Assert.NotEqual(first, second);
        Assert.DoesNotContain('+', first);
        Assert.DoesNotContain('/', first);
        Assert.DoesNotContain('=', first);
        Assert.InRange(firstExpiry, DateTime.UtcNow.AddDays(29), DateTime.UtcNow.AddDays(31));
        Assert.InRange(secondExpiry, DateTime.UtcNow.AddDays(29), DateTime.UtcNow.AddDays(31));
    }
}
