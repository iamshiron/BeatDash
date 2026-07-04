namespace Shiron.BeatDash.DB.Schema;

public class PlaySessionNoteItem : PlaySessionItem {
    public required ColorType ColorType { get; set; }
    public required NoteType NoteType { get; set; }
    public required NoteScoringType ScoringType { get; set; }
    public required CutDirection CutDirection { get; set; }
    public required int LineIndex { get; set; }
    public required int NoteLineLayer { get; set; }
    public required int Result { get; set; }
    public required int MaxScore { get; set; }

    /// <summary>
    /// Earned before-cut points (0–70). Persisted directly so the exact per-note
    /// score is recoverable without reconstructing from ratings.
    /// </summary>
    public required int BeforeCutScore { get; set; }

    /// <summary>
    /// Earned center-accuracy points (0–15). This is the rounded output score,
    /// not the raw <c>cutDistanceToCenter</c> input.
    /// </summary>
    public required int CenterDistanceScore { get; set; }

    /// <summary>
    /// Earned after-cut points (0–30). Persisted directly so the exact per-note
    /// score is recoverable without reconstructing from ratings.
    /// </summary>
    public required int AfterCutScore { get; set; }

    /// <summary>Pre-cut swing rating (0–1), the input to <see cref="BeforeCutScore"/>.</summary>
    public required float PreCutSwing { get; set; }
    /// <summary>Post-cut swing rating (0–1), the input to <see cref="AfterCutScore"/>.</summary>
    public required float PostCutSwing { get; set; }

    /// <summary>
    /// Magnitude of the world-space cut point (<c>|cutInfo.cutPoint|</c>). NOT
    /// the accuracy metric (<c>cutDistanceToCenter</c>); kept only as raw
    /// kinematics. Do not treat as an accuracy measure.
    /// </summary>
    public required float CutPointDistance { get; set; }
    public required float SaberSpeed { get; set; }
}
