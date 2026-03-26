namespace Shiron.BeatDash.API.DTOs;

public record MapFilterParams : PaginationParams {
    public string? Search { get; init; }
    public string? SongAuthor { get; init; }
    public string? Mapper { get; init; }
    public int? MinBPM { get; init; }
    public int? MaxBPM { get; init; }
    public int? MinDuration { get; init; }
    public int? MaxDuration { get; init; }
    public string? BSRKey { get; init; }
    public bool? HasBeenPlayed { get; init; }
}

public record MapDto {
    public long Id { get; init; }
    public string Hash { get; init; } = "";
    public string SongName { get; init; } = "";
    public string SongSubName { get; init; } = "";
    public string SongAuthor { get; init; } = "";
    public string Mapper { get; init; } = "";
    public string? BSRKey { get; init; }
    public int Duration { get; init; }
    public int BPM { get; init; }
    public bool HasCoverImage { get; init; }
    public string GameVersion { get; init; } = "";
    public int PlayCount { get; init; }
    public List<DifficultyDto> Difficulties { get; init; } = [];
}

public record MapSummaryDto {
    public long Id { get; init; }
    public string Hash { get; init; } = "";
    public string SongName { get; init; } = "";
    public string SongSubName { get; init; } = "";
    public string SongAuthor { get; init; } = "";
    public string Mapper { get; init; } = "";
    public string? BSRKey { get; init; }
    public int Duration { get; init; }
    public int BPM { get; init; }
    public bool HasCoverImage { get; init; }
    public int PlayCount { get; init; }
    public int DifficultyCount { get; init; }
}
