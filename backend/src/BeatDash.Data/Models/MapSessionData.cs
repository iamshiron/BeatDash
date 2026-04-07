namespace Shiron.BeatDash.Data.Models;

public class MapSessionData {
    public string? Hash { get; set; }
    public string SongName { get; set; } = "";
    public string SongSubName { get; set; } = "";
    public string SongAuthor { get; set; } = "";
    public string Mapper { get; set; } = "";
    public string? BSRKey { get; set; }
    public string? CoverImage { get; set; }
    public int Duration { get; set; }
    public string MapType { get; set; } = "";
    public string Difficulty { get; set; } = "";
    public string? CustomDifficultyLabel { get; set; }
    public int BPM { get; set; }
    public double NJS { get; set; }
    public Modifiers Modifiers { get; set; } = new();
    public float ModifiersMultiplier { get; set; } = 1.0f;
    public bool PracticeMode { get; set; }
    public PracticeModeModifiers PracticeModeModifiers { get; set; } = new();
    public double PP { get; set; }
    public double Star { get; set; }
    public string GameVersion { get; set; } = "";
    public string PluginVersion { get; set; } = "";
    public bool IsMultiplayer { get; set; }
    public int PreviousRecord { get; set; }
    public string? PreviousBSR { get; set; }

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public bool NoFailEnabled { get; set; }
    public bool IsPaused { get; set; }
    public MapEndReason? EndReason { get; set; }
    public bool FullCleared { get; set; }

    public List<LiveDataSnapshot> Snapshots { get; set; } = [];

    public int FinalScore { get; set; }
    public int FinalScoreWithMultipliers { get; set; }
    public int MaxScore { get; set; }
    public int MaxScoreWithMultipliers { get; set; }
    public double FinalAccuracy { get; set; }
    public string FinalRank { get; set; } = "";
    public bool FullCombo { get; set; } = true;
    public int FinalCombo { get; set; }
    public int FinalMisses { get; set; }
    public int NotesSpawned { get; set; }
    public double FinalPlayerHealth { get; set; }
    public BlockHitScore? FinalBlockHitScore { get; set; }
    public int DurationPlayed { get; set; }

    public IReadOnlyList<LiveDataSnapshot> ScoreOverTime => Snapshots.AsReadOnly();
    public double MinHealth => Snapshots.Count > 0 ? Snapshots.Min(s => s.PlayerHealth) : 0;
    public double MaxAccuracy => Snapshots.Count > 0 ? Snapshots.Max(s => s.Accuracy) : 0;
    public double MinAccuracy => Snapshots.Count > 0 ? Snapshots.Min(s => s.Accuracy) : 0;
    public int MaxCombo => Snapshots.Count > 0 ? Snapshots.Max(s => s.Combo) : 0;
    public bool WasFullComboThroughout => Snapshots.Count == 0 || Snapshots.All(s => s.FullCombo);
    public int TotalNotesHit => NotesSpawned - FinalMisses;
    public double AccuracyDelta => Snapshots.Count >= 2 ? Snapshots.Last().Accuracy - Snapshots.First().Accuracy : 0;
    public TimeSpan SessionDuration => (EndedAt ?? DateTimeOffset.UtcNow) - StartedAt;
}
