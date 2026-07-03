namespace Shiron.BeatDash.Data.Socket;

/// <summary>
/// Beat Saber letter grades, encoded as a single byte in binary packets.
/// </summary>
public enum Grade : byte {
    SS = 0,
    S = 1,
    A = 2,
    B = 3,
    C = 4,
    D = 5,
    E = 6,
}
