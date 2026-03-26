namespace Shiron.BeatDash.API.DTOs;

public record PlaySessionDto {
    public long Id { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
    public string? EndReason { get; init; }
    public double DurationSeconds => FinishedAt.HasValue ? (FinishedAt.Value - StartedAt).TotalSeconds : 0;

    public MapSummaryDto Map { get; init; } = null!;
    public DifficultyDto Difficulty { get; init; } = null!;

    public float ModifiersMultiplier { get; init; }
    public bool PracticeMode { get; init; }
    public string PluginVersion { get; init; } = "";
    public bool IsMultiplayer { get; init; }
    public int PreviousRecord { get; init; }
    public string? PreviousBSR { get; init; }

    public ModifiersDto Modifiers { get; init; } = null!;
    public PracticeModeModifiersDto PracticeModeModifiers { get; init; } = null!;

    public int FinalScore { get; init; }
    public int FinalScoreWithMultipliers { get; init; }
    public int FinalMaxScore { get; init; }
    public int FinalMaxScoreWithMultipliers { get; init; }
    public string? FinalRank { get; init; }
    public bool FinalFullCombo { get; init; }
    public int FinalCombo { get; init; }
    public int FinalMisses { get; init; }
    public double FinalAccuracy { get; init; }
    public int FinalTimeElapsed { get; init; }

    public double ScorePercentage => FinalMaxScore > 0 ? (double) FinalScore / FinalMaxScore * 100 : 0;
    public double ScorePercentageWithMultipliers => FinalMaxScoreWithMultipliers > 0 ? (double) FinalScoreWithMultipliers / FinalMaxScoreWithMultipliers * 100 : 0;
}

public record PlaySessionSummaryDto {
    public long Id { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
    public string? EndReason { get; init; }
    public double DurationSeconds => FinishedAt.HasValue ? (FinishedAt.Value - StartedAt).TotalSeconds : 0;

    public long MapId { get; init; }
    public string SongName { get; init; } = "";
    public string SongAuthor { get; init; } = "";
    public string Mapper { get; init; } = "";
    public bool HasCoverImage { get; init; }
    public string Difficulty { get; init; } = "";
    public string MapType { get; init; } = "";

    public int FinalScore { get; init; }
    public int FinalMaxScore { get; init; }
    public string? FinalRank { get; init; }
    public double FinalAccuracy { get; init; }
    public bool FinalFullCombo { get; init; }
    public int FinalMisses { get; init; }

    public double ScorePercentage => FinalMaxScore > 0 ? (double) FinalScore / FinalMaxScore * 100 : 0;
}

public record PlaySessionFilterParams : PaginationParams {
    public long? MapId { get; init; }
    public string? MapHash { get; init; }
    public string? EndReason { get; init; }
    public DateTimeOffset? FromDate { get; init; }
    public DateTimeOffset? ToDate { get; init; }
    public bool? PracticeMode { get; init; }
    public bool? Multiplayer { get; init; }
    public string? Difficulty { get; init; }
    public string? MapType { get; init; }
    public bool? FullCombo { get; init; }
    public double? MinAccuracy { get; init; }
    public double? MaxAccuracy { get; init; }
}

public record ModifiersDto {
    public bool NoFailOn0Energy { get; init; }
    public bool OneLife { get; init; }
    public bool FourLives { get; init; }
    public bool NoBombs { get; init; }
    public bool NoWalls { get; init; }
    public bool NoArrows { get; init; }
    public bool GhostNotes { get; init; }
    public bool DisappearingArrows { get; init; }
    public bool SmallNotes { get; init; }
    public bool ProMode { get; init; }
    public bool StrictAngles { get; init; }
    public bool ZenMode { get; init; }
    public bool SlowerSong { get; init; }
    public bool FasterSong { get; init; }
    public bool SuperFastSong { get; init; }
}

public record PracticeModeModifiersDto {
    public float SongSpeedMul { get; init; }
    public bool StartInAdvanceAndClearNotes { get; init; }
    public float SongStartTime { get; init; }
}
