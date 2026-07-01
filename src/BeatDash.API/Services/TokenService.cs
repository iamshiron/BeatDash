using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Shiron.BeatDash.DB.Schema;
using JwtRegisteredClaimNames = Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames;

namespace Shiron.BeatDash.API.Services;

public interface ITokenService {
    string GenerateAccessToken(Guid userId, out DateTime expires);
    string GenerateRefreshToken(out DateTime expires);
    ClaimsPrincipal? FromExpiredToken(string accessToken);
}

public class TokenService(string secretKey, string issuer, string audience) : ITokenService {
    public string GenerateAccessToken(Guid userId, out DateTime expires) {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        List<Claim> claims = [
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        ];

        expires = DateTime.UtcNow.AddMinutes(15);
        var tokenDescriptor = new SecurityTokenDescriptor {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
    public string GenerateRefreshToken(out DateTime expires) {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);

        expires = DateTime.UtcNow.AddDays(30);

        return Convert.ToBase64String(randomNumber)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    public ClaimsPrincipal? FromExpiredToken(string accessToken) {
        var tokenValidationParams = new TokenValidationParameters {
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateLifetime = false,
            ValidIssuer = issuer,
            ValidAudience = audience
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        if (!tokenHandler.CanReadToken(accessToken)) {
            return null;
        }

        try {
            var principal = tokenHandler.ValidateToken(accessToken, tokenValidationParams, out var securityToken);

            var jwtSecurityToken = securityToken as JwtSecurityToken;
            if (jwtSecurityToken == null || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase)) {
                return null;
            }

            return principal;
        } catch (SecurityTokenException) {
            return null;
        }
    }
}
