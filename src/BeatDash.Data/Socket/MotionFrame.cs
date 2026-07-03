using System.Runtime.InteropServices;

namespace Shiron.BeatDash.Data.Socket;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct TransformData {
    public readonly float PosX, PosY, PosZ;
    public readonly float RotX, RotY, RotZ, RotW;

    public TransformData(float posX, float posY, float posZ, float rotX, float rotY, float rotZ, float rotW) {
        PosX = posX; PosY = posY; PosZ = posZ;
        RotX = rotX; RotY = rotY; RotZ = rotZ; RotW = rotW;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct MotionFrame {
    public readonly float SongTime;
    public readonly TransformData LeftSaber;
    public readonly TransformData RightSaber;
    public readonly TransformData Head;

    public MotionFrame(float songTime, TransformData leftSaber, TransformData rightSaber, TransformData head) {
        SongTime = songTime;
        LeftSaber = leftSaber;
        RightSaber = rightSaber;
        Head = head;
    }

    public const int Size = 88;
}
