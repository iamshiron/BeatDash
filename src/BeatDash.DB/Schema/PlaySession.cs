namespace Shiron.BeatDash.DB.Schema;

public class PlaySession {
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required int CorrelationId { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid BeatmapDifficultyId { get; set; }
    public BeatmapDifficulty BeatmapDifficulty { get; set; }

    public required DateTime StartedAt { get; set; }
    public required DateTime? EndedAt { get; set; }

    /// <summary>
    /// Whether the session was played with auto-play active. Client-reported.
    /// </summary>
    public bool AutoMode { get; set; }

    /// <summary>Final results, populated when the session reaches a terminal state.</summary>
    public PlaySessionResults? Results { get; set; }

    // Relations
    public IList<PlaySessionNoteItem> NoteItems { get; set; } = [];
    public IList<PlaySessionComboBreakItem> ComboBreakItems { get; set; } = [];
    public IList<PlaySessionEnergyChangeItem> EnergyChangeItems { get; set; } = [];
    public IList<PlaySessionScoreChangeItem> ScoreChangeItems { get; set; } = [];
    public IList<PlaySessionItemMotionFrame> MotionFrameItems { get; set; } = [];
}
