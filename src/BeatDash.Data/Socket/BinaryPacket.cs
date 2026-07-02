namespace Shiron.BeatDash.Data.Socket;

public class BinaryPacket {
    public BinaryPacket(BinaryPacketTypes type, byte[] data) {
        Payload = new byte[data.Length + 1];
        Payload[0] = (byte) type;
        data.CopyTo(Payload, 1);
    }

    public byte[] Payload { get; init; }
}
