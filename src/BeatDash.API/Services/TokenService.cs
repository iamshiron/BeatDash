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

public class TokenService(IConfiguration config) : ITokenService {
    private readonly string _secretKey = config.GetSection("Jwt")["SecretKey"] ?? throw new InvalidOperationException("JWT secret key not configured");
    private readonly string _issuer = config.GetSection("Jwt")["Issuer"] ?? throw new InvalidOperationException("JWT issuer not configured");
    private readonly string _audience = config.GetSection("Jwt")["Audience"] ?? throw new InvalidOperationException("JWT audience not configured");

    public string GenerateAccessToken(Guid userId, out DateTime expires) {
        Console.WriteLine($"Secret key: {_secretKey}");
        Console.WriteLine($"Issuer: {_issuer}");
        Console.WriteLine($"Audience: {_audience}");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        List<Claim> claims = [
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        ];

        expires = DateTime.UtcNow.AddMinutes(15);
        var tokenDescriptor = new SecurityTokenDescriptor {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
    public string GenerateRefreshToken(out DateTime expires) {
        Console.WriteLine($"Secret key: {_secretKey}");
        Console.WriteLine($"Issuer: {_issuer}");
        Console.WriteLine($"Audience: {_audience}");

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
        Console.WriteLine($"Secret key: {_secretKey}");
        Console.WriteLine($"Issuer: {_issuer}");
        Console.WriteLine($"Audience: {_audience}");

        var tokenValidationParams = new TokenValidationParameters {
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey)),
            ValidateLifetime = false,
            ValidIssuer = _issuer,
            ValidAudience = _audience
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var principal = tokenHandler.ValidateToken(accessToken, tokenValidationParams, out var securityToken);

        var jwtSecurityToken = securityToken as JwtSecurityToken;
        if (jwtSecurityToken == null || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase)) {
            throw new SecurityTokenExpiredException("Invalid token");
        }

        return principal;
    }
}
