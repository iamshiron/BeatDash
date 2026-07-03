namespace Shiron.BeatDash.Data.Socket;

public enum BinaryPacketTypes : byte {
    MapCoverImage = 0x01,
    MotionFrameBatch = 0x02,

    /// <summary>
    /// UDP-only handshake: a 16-byte ticket used to bind a UDP endpoint to a
    /// session before any other packet type is accepted over UDP.
    /// </summary>
    Holepunch = 0x03,

    /// <summary>Frequent score snapshot — UDP-eligible.</summary>
    ScoreUpdate = 0x04,

    /// <summary>Map-start metadata — reliable (TCP).</summary>
    MapStart = 0x06,

    /// <summary>Map state change (paused/resumed/finished/failed/quit) — reliable (TCP).</summary>
    MapState = 0x07,
}
