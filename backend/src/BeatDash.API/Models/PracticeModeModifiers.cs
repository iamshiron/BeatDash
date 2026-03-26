namespace Shiron.BeatDash.API.Models;

public class PracticeModeModifiers {
    public float SongSpeedMul { get; set; } = 1.0f;
    public bool StartInAdvanceAndClearNotes { get; set; }
    public float SongStartTime { get; set; }
}
