namespace Shiron.BeatDash.API.DTOs;

public record AnalyticsOverviewDto {
    public int TotalPlaySessions { get; init; }
    public int TotalMapsPlayed { get; init; }
    public int UniqueMaps { get; init; }
    public int TotalPlayTimeSeconds { get; init; }
    public double TotalPlayTimeHours => TotalPlayTimeSeconds / 3600.0;

    public double AverageAccuracy { get; init; }
    public double AverageScorePercentage { get; init; }
    public int FullComboCount { get; init; }
    public double FullComboRate => TotalPlaySessions > 0 ? (double) FullComboCount / TotalPlaySessions * 100 : 0;

    public int FinishedCount { get; init; }
    public int FailedCount { get; init; }
    public int QuitCount { get; init; }

    public int SSSCount { get; init; }
    public int SCount { get; init; }
    public int ACount { get; init; }
    public int BCount { get; init; }
    public int CCount { get; init; }
    public int DCount { get; init; }
    public int ECount { get; init; }

    public PlaySessionSummaryDto? BestSession { get; init; }
    public PlaySessionSummaryDto? MostRecentSession { get; init; }

    public DateTimeOffset? FirstPlayDate { get; init; }
    public DateTimeOffset? LastPlayDate { get; init; }
}

public record PerformanceTrendDto {
    public DateTimeOffset Date { get; init; }
    public int PlayCount { get; init; }
    public double AverageAccuracy { get; init; }
    public double AverageScorePercentage { get; init; }
    public int TotalPlayTimeSeconds { get; init; }
    public int FullComboCount { get; init; }
    public int FinishedCount { get; init; }
    public int FailedCount { get; init; }
}

public record PerformanceTrendFilterParams {
    public DateTimeOffset? FromDate { get; init; }
    public DateTimeOffset? ToDate { get; init; }
    public string GroupBy { get; init; } = "day";
}

public record MapPlayStatsDto {
    public long MapId { get; init; }
    public string SongName { get; init; } = "";
    public string SongAuthor { get; init; } = "";
    public string Mapper { get; init; } = "";
    public bool HasCoverImage { get; init; }
    public string? BSRKey { get; init; }
    public int PlayCount { get; init; }
    public double AverageAccuracy { get; init; }
    public double AverageScorePercentage { get; init; }
    public int FullComboCount { get; init; }
    public int BestScore { get; init; }
    public double BestScorePercentage { get; init; }
    public string? BestRank { get; init; }
    public DateTimeOffset LastPlayedAt { get; init; }
}

public record DifficultyStatsDto {
    public string Difficulty { get; init; } = "";
    public int PlayCount { get; init; }
    public double AverageAccuracy { get; init; }
    public double AverageScorePercentage { get; init; }
    public int FullComboCount { get; init; }
    public int FinishedCount { get; init; }
    public int FailedCount { get; init; }
}

public record MapTypeStatsDto {
    public string MapType { get; init; } = "";
    public int PlayCount { get; init; }
    public double AverageAccuracy { get; init; }
    public double AverageScorePercentage { get; init; }
    public int FullComboCount { get; init; }
}

public record ScoreDistributionDto {
    public string Rank { get; init; } = "";
    public int Count { get; init; }
    public double Percentage { get; init; }
}

public record HourlyPlayStatsDto {
    public int Hour { get; init; }
    public int PlayCount { get; init; }
    public double AverageAccuracy { get; init; }
}

public record DailyPlayStatsDto {
    public int DayOfWeek { get; init; }
    public string DayName { get; init; } = "";
    public int PlayCount { get; init; }
    public double AverageAccuracy { get; init; }
}

public record AccuracyDistributionDto {
    public string Range { get; init; } = "";
    public int Count { get; init; }
    public double Percentage { get; init; }
}

public record SessionPerformanceBreakdownDto {
    public long PlaySessionId { get; init; }
    public List<LiveDataSnapshotDto> Snapshots { get; init; } = [];
    public AccuracyOverTimeDto AccuracyOverTime { get; init; } = null!;
    public ScoreOverTimeDto ScoreOverTime { get; init; } = null!;
    public HealthOverTimeDto HealthOverTime { get; init; } = null!;
    public ComboOverTimeDto ComboOverTime { get; init; } = null!;
}

public record AccuracyOverTimeDto {
    public List<TimePointDto> Points { get; init; } = [];
}

public record ScoreOverTimeDto {
    public List<TimePointDto> Points { get; init; } = [];
}

public record HealthOverTimeDto {
    public List<TimePointDto> Points { get; init; } = [];
}

public record ComboOverTimeDto {
    public List<ComboPointDto> Points { get; init; } = [];
}

public record TimePointDto {
    public int TimeElapsed { get; init; }
    public double Value { get; init; }
}

public record ComboPointDto {
    public int TimeElapsed { get; init; }
    public int Combo { get; init; }
    public int Misses { get; init; }
}
