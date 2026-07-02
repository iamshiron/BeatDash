using System;

namespace Shiron.BeatDash.Data.Socket;

public class BinaryPacket {
    public BinaryPacket(BinaryPacketTypes type, byte[] data) {
        Payload = new byte[5 + data.Length];
        var contentSize = 1 + data.Length;
        var lengthBytes = BitConverter.GetBytes(contentSize);

        lengthBytes.CopyTo(Payload, 0);
        Payload[4] = (byte) type;

        data.CopyTo(Payload, 5);
    }

    public byte[] Payload { get; init; }
}
