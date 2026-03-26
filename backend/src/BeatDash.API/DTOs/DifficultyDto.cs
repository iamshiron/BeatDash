namespace Shiron.BeatDash.API.DTOs;

public record DifficultyDto {
    public long Id { get; init; }
    public long MapId { get; init; }
    public string MapType { get; init; } = "";
    public string Difficulty { get; init; } = "";
    public string? CustomDifficultyLabel { get; init; }
    public double NJS { get; init; }
    public double PP { get; init; }
    public double Star { get; init; }
    public int PlayCount { get; init; }
}

public record DifficultySummaryDto {
    public long Id { get; init; }
    public string MapType { get; init; } = "";
    public string Difficulty { get; init; } = "";
    public string? CustomDifficultyLabel { get; init; }
    public double NJS { get; init; }
    public double PP { get; init; }
    public double Star { get; init; }
}
