namespace Shiron.BeatDash.API.Data.Entities;

public class LiveDataSnapshotEntity {
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public long PlaySessionId { get; set; }
    public PlaySessionEntity PlaySession { get; set; } = null!;

    public int Score { get; set; }
    public int ScoreWithMultipliers { get; set; }
    public int MaxScore { get; set; }
    public int MaxScoreWithMultipliers { get; set; }
    public string Rank { get; set; } = "SSS";
    public bool FullCombo { get; set; } = true;
    public int NotesSpawned { get; set; }
    public int Combo { get; set; }
    public int Misses { get; set; }
    public double Accuracy { get; set; } = 100;
    public double PlayerHealth { get; set; } = 50;
    public int TimeElapsed { get; set; }
    public int EventTrigger { get; set; }

    public int BlockHitPreSwing { get; set; }
    public int BlockHitPostSwing { get; set; }
    public int BlockHitCenterSwing { get; set; }
    public int NoteColorType { get; set; }
}
