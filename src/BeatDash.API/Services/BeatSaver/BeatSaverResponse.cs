using System.Text.Json.Serialization;

namespace Shiron.BeatDash.API.Services.BeatSaver;

/// <summary>
/// Deserialized shape of the BeatSaver <c>GET /maps/hash/{hash}</c> response.
/// Property matching is case-insensitive, so <c>downloadURL</c> maps onto
/// <see cref="BeatSaverVersionResponse.DownloadUrl"/> without explicit names.
/// </summary>
public sealed record BeatSaverMapResponse {
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }

    public BeatSaverUserResponse? Uploader { get; init; }
    public BeatSaverMetadataResponse? Metadata { get; init; }
    public BeatSaverStatsResponse? Stats { get; init; }

    public DateTimeOffset? Uploaded { get; init; }
    public bool Automapper { get; init; }
    public bool Ranked { get; init; }
    public bool Qualified { get; init; }

    public IReadOnlyList<BeatSaverVersionResponse>? Versions { get; init; }

    public DateTimeOffset? CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public DateTimeOffset? LastPublishedAt { get; init; }

    public IReadOnlyList<string>? Tags { get; init; }
    public string? DeclaredAi { get; init; }
    public bool BlRanked { get; init; }
    public bool BlQualified { get; init; }
}

public sealed record BeatSaverUserResponse {
    public long Id { get; init; }
    public string? Name { get; init; }
    public string? Hash { get; init; }
    public string? Avatar { get; init; }
    public string? Type { get; init; }
    public bool Admin { get; init; }
    public bool Curator { get; init; }
    public bool SeniorCurator { get; init; }
    public string? PlaylistUrl { get; init; }
}

public sealed record BeatSaverMetadataResponse {
    public float Bpm { get; init; }
    public int Duration { get; init; }
    public string? SongName { get; init; }
    public string? SongSubName { get; init; }
    public string? SongAuthorName { get; init; }
    public string? LevelAuthorName { get; init; }
}

public sealed record BeatSaverStatsResponse {
    public int Plays { get; init; }
    public int Downloads { get; init; }
    public int Upvotes { get; init; }
    public int Downvotes { get; init; }
    public float Score { get; init; }
}

public sealed record BeatSaverVersionResponse {
    public string? Hash { get; init; }
    public string? Key { get; init; }
    public string? State { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
    public int? SageScore { get; init; }
    public IReadOnlyList<BeatSaverDiffResponse>? Diffs { get; init; }
    public string? DownloadUrl { get; init; }
    public string? CoverUrl { get; init; }
    public string? PreviewUrl { get; init; }
}

public sealed record BeatSaverDiffResponse {
    public float Njs { get; init; }
    public float Offset { get; init; }
    public int Notes { get; init; }
    public int Bombs { get; init; }
    public int Obstacles { get; init; }
    public float Nps { get; init; }
    public float Length { get; init; }
    public string? Characteristic { get; init; }
    public string? Difficulty { get; init; }
    public int Events { get; init; }
    public bool Chroma { get; init; }

    [JsonPropertyName("me")] public bool MappingExtensions { get; init; }
    [JsonPropertyName("ne")] public bool NoodleExtensions { get; init; }

    public bool Cinema { get; init; }
    public float Seconds { get; init; }
    public BeatSaverParitySummaryResponse? ParitySummary { get; init; }
    public int? MaxScore { get; init; }
    public string? Environment { get; init; }
}

public sealed record BeatSaverParitySummaryResponse {
    public int Errors { get; init; }
    public int Warns { get; init; }
    public int Resets { get; init; }
}
