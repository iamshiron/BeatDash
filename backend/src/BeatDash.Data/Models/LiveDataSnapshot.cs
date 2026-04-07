namespace Shiron.BeatDash.Data.Models;

public class LiveDataSnapshot {
    public DateTimeOffset Timestamp { get; set; }
    public int TimeElapsed { get; set; }
    public int Score { get; set; }
    public int ScoreWithMultipliers { get; set; }
    public int MaxScore { get; set; }
    public int MaxScoreWithMultipliers { get; set; }
    public double Accuracy { get; set; }
    public double PlayerHealth { get; set; }
    public int Combo { get; set; }
    public int Misses { get; set; }
    public int NotesSpawned { get; set; }
    public bool FullCombo { get; set; }
    public string Rank { get; set; } = "SSS";
    public BlockHitScore BlockHitScore { get; set; } = new();
}
