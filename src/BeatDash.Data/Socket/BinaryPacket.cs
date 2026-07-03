using System;

namespace Shiron.BeatDash.Data.Socket;

/// <summary>
/// Builds the binary packet envelope used over the TCP WebSocket: a 4-byte
/// little-endian content-length prefix, a 1-byte <see cref="BinaryPacketTypes"/>
/// type, then the payload. UDP uses the leaner <see cref="UdpPacket"/> envelope.
/// </summary>
public class BinaryPacket {
    /// <summary>
    /// Size of the TCP packet header, in bytes: 4-byte length prefix + 1-byte type.
    /// </summary>
    public const int HeaderSize = 5;

    public BinaryPacket(BinaryPacketTypes type, byte[] data) {
        Payload = new byte[HeaderSize + data.Length];
        var contentSize = 1 + data.Length;
        var lengthBytes = BitConverter.GetBytes(contentSize);

        lengthBytes.CopyTo(Payload, 0);
        Payload[HeaderSize - 1] = (byte) type;

        data.CopyTo(Payload, HeaderSize);
    }

    public byte[] Payload { get; init; }
}
