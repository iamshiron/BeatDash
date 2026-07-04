namespace Shiron.BeatDash.DB.Schema;

/// <summary>
/// Required swing direction of a note, mirroring Beat Saber's
/// <c>NoteCutDirection</c>.
/// </summary>
public enum CutDirection {
    Up = 0,
    Down = 1,
    Left = 2,
    Right = 3,
    UpLeft = 4,
    UpRight = 5,
    DownLeft = 6,
    DownRight = 7,
    Any = 8,
    None = 9,
}
