namespace Shiron.BeatDash.API.Models;

public class LiveData {
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
    public BlockHitScore BlockHitScore { get; set; } = new();
    public double PlayerHealth { get; set; } = 50;
    public NoteColorType ColorType { get; set; } = NoteColorType.None;
    public int TimeElapsed { get; set; }
    public LiveDataEventTrigger EventTrigger { get; set; } = LiveDataEventTrigger.Unknown;
}

public class BlockHitScore {
    public int PreSwing { get; set; }
    public int PostSwing { get; set; }
    public int CenterSwing { get; set; }
}

public enum NoteColorType {
    ColorA = 0,
    ColorB = 1,
    None = -1
}

public enum LiveDataEventTrigger {
    Unknown = 0,
    TimerElapsed,
    NoteMissed,
    EnergyChange,
    ScoreChange
}
