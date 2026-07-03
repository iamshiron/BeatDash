namespace Shiron.BeatDash.Data.Socket;

/// <summary>
/// Builds the binary packet envelope used over the UDP socket: a single leading
/// <see cref="BinaryPacketTypes"/> type byte followed by the payload. No length
/// prefix is required because UDP datagrams are atomic and self-framing, unlike
/// the length-prefixed <see cref="BinaryPacket"/> used over the TCP stream.
/// </summary>
public static class UdpPacket {
    /// <summary>
    /// Size of the UDP packet header, in bytes (the type byte only).
    /// </summary>
    public const int HeaderSize = 1;

    /// <summary>
    /// Builds a UDP packet from a type and payload.
    /// </summary>
    public static byte[] Build(BinaryPacketTypes type, byte[] payload) {
        var result = new byte[HeaderSize + payload.Length];
        result[HeaderSize - 1] = (byte) type;
        payload.CopyTo(result, HeaderSize);
        return result;
    }
}
