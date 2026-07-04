namespace Shiron.BeatDash.DB.Schema;

/// <summary>
/// Scoring classification of a note, mirroring Beat Saber's
/// <c>NoteData.ScoringType</c>. Several of these share a
/// <see cref="NoteType"/> of <see cref="NoteType.Normal"/> but pin different
/// cut-score ranges (e.g. arc/Slider endpoints), so this is required to
/// reconstruct the exact per-note score from the stored swing ratings.
/// </summary>
public enum NoteScoringType {
    NoScoring = 0,
    Normal = 1,
    SliderHead = 2,
    SliderTail = 3,
    BurstSliderHead = 4,
    BurstSliderLink = 5,
}
