namespace Shiron.BeatDash.Data.Socket;

/// <summary>
/// Bit indices for all gameplay modifiers packed into a single integer bitmask.
/// Boolean modifiers and enum values each occupy their own bit, no multi-bit packing.
/// </summary>
public enum ModifierBit {
    NoFailOn0Energy = 0,
    InstaFail = 1,
    FailOnSaberClash = 2,
    NoBombs = 3,
    FastNotes = 4,
    StrictAngles = 5,
    DisappearingArrows = 6,
    GhostNotes = 7,
    NoArrows = 8,
    ProMode = 9,
    ZenMode = 10,
    SmallCubes = 11,

    EnergyType_Bar = 12,
    EnergyType_Battery = 13,

    Obstacles_All = 14,
    Obstacles_FullHeightOnly = 15,
    Obstacles_NoObstacles = 16,

    SongSpeed_Normal = 17,
    SongSpeed_Slower = 18,
    SongSpeed_Faster = 19,
    SongSpeed_SuperFast = 20
}
