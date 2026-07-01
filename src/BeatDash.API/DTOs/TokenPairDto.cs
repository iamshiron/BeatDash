namespace Shiron.BeatDash.API.DTOs;

public record TokenPairDto {
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
}

public record TokenPairExpiryDto : TokenPairDto {
    public required DateTime AccessExpiresAt { get; init; }
    public required DateTime RefreshExpiresAt { get; init; }
}

public sealed record RefreshTokenRequestDto {
    public required Guid ClientId { get; init; }
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
}
