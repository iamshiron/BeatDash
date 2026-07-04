namespace Shiron.BeatDash.DB.Schema;

/// <summary>
/// Gameplay classification of a note, mirroring Beat Saber's
/// <c>NoteData.GameplayType</c>.
/// </summary>
public enum NoteType {
    Normal = 0,
    Bomb = 1,
    BurstSliderHead = 2,
    BurstSliderElement = 3,
}
