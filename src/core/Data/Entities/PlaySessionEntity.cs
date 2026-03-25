namespace Shiron.BeatDash.API.Data.Entities;

public class PlaySessionEntity {
    public long Id { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    public string? EndReason { get; set; }

    public long MapId { get; set; }
    public MapEntity Map { get; set; } = null!;

    public long DifficultyId { get; set; }
    public DifficultyEntity Difficulty { get; set; } = null!;

    public float ModifiersMultiplier { get; set; } = 1.0f;
    public bool PracticeMode { get; set; }

    public string PluginVersion { get; set; } = "";
    public bool IsMultiplayer { get; set; }
    public int PreviousRecord { get; set; }
    public string? PreviousBSR { get; set; }

    public ModifiersEntity Modifiers { get; set; } = new();
    public PracticeModeModifiersEntity PracticeModeModifiers { get; set; } = new();

    public int FinalScore { get; set; }
    public int FinalScoreWithMultipliers { get; set; }
    public int FinalMaxScore { get; set; }
    public int FinalMaxScoreWithMultipliers { get; set; }
    public string? FinalRank { get; set; }
    public bool FinalFullCombo { get; set; }
    public int FinalCombo { get; set; }
    public int FinalMisses { get; set; }
    public double FinalAccuracy { get; set; }
    public int FinalTimeElapsed { get; set; }

    public ICollection<LiveDataSnapshotEntity> LiveDataSnapshots { get; set; } = [];
}

public class ModifiersEntity {
    public long Id { get; set; }
    public bool NoFailOn0Energy { get; set; }
    public bool OneLife { get; set; }
    public bool FourLives { get; set; }
    public bool NoBombs { get; set; }
    public bool NoWalls { get; set; }
    public bool NoArrows { get; set; }
    public bool GhostNotes { get; set; }
    public bool DisappearingArrows { get; set; }
    public bool SmallNotes { get; set; }
    public bool ProMode { get; set; }
    public bool StrictAngles { get; set; }
    public bool ZenMode { get; set; }
    public bool SlowerSong { get; set; }
    public bool FasterSong { get; set; }
    public bool SuperFastSong { get; set; }
}

public class PracticeModeModifiersEntity {
    public long Id { get; set; }
    public float SongSpeedMul { get; set; } = 1.0f;
    public bool StartInAdvanceAndClearNotes { get; set; }
    public float SongStartTime { get; set; }
}
