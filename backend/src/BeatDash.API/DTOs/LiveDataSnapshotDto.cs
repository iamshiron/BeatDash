namespace Shiron.BeatDash.API.DTOs;

public record LiveDataSnapshotDto {
    public long Id { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public long PlaySessionId { get; init; }

    public int Score { get; init; }
    public int ScoreWithMultipliers { get; init; }
    public int MaxScore { get; init; }
    public int MaxScoreWithMultipliers { get; init; }
    public string Rank { get; init; } = "SSS";
    public bool FullCombo { get; init; }
    public int NotesSpawned { get; init; }
    public int Combo { get; init; }
    public int Misses { get; init; }
    public double Accuracy { get; init; }
    public double PlayerHealth { get; init; }
    public int TimeElapsed { get; init; }
    public int EventTrigger { get; init; }
    public string EventTriggerName => EventTrigger switch {
        0 => "TimerElapsed",
        1 => "NoteMissed",
        2 => "EnergyChange",
        3 => "ScoreChange",
        _ => "Unknown"
    };

    public int BlockHitPreSwing { get; init; }
    public int BlockHitPostSwing { get; init; }
    public int BlockHitCenterSwing { get; init; }
    public int NoteColorType { get; init; }
    public string NoteColorName => NoteColorType switch {
        0 => "ColorA",
        1 => "ColorB",
        2 => "None",
        _ => "Unknown"
    };

    public double ScorePercentage => MaxScore > 0 ? (double) Score / MaxScore * 100 : 0;
}

public record LiveDataSnapshotFilterParams : PaginationParams {
    public long? PlaySessionId { get; init; }
    public DateTimeOffset? FromTimestamp { get; init; }
    public DateTimeOffset? ToTimestamp { get; init; }
    public int? EventTrigger { get; init; }
    public bool? FullCombo { get; init; }
    public double? MinAccuracy { get; init; }
    public double? MaxAccuracy { get; init; }
}
