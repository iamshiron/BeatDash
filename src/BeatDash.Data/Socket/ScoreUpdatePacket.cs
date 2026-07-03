using System.Runtime.InteropServices;

namespace Shiron.BeatDash.Data.Socket;

/// <summary>
/// Fixed 33-byte binary packet for real-time score updates.
/// Wire format is little-endian, matching the sequential packed layout below.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct ScoreUpdatePacket {
    /// <summary>Fixed payload size in bytes.</summary>
    public const int Size = 33;

    /// <summary>Correlation ID linking this update to a map session.</summary>
    public readonly int CorrelationId;

    /// <summary>Position in the song timeline (seconds).</summary>
    public readonly float SongTime;

    /// <summary>Current cumulative modified score.</summary>
    public readonly int Score;

    /// <summary>Maximum possible modified score at this point.</summary>
    public readonly int MaxScore;

    /// <summary>Accuracy ratio (0–1): Score / MaxScore.</summary>
    public readonly float Accuracy;

    /// <summary>Letter grade computed from accuracy.</summary>
    public readonly Grade Grade;

    /// <summary>Saber energy / health (0–1).</summary>
    public readonly float Energy;

    /// <summary>Current active combo.</summary>
    public readonly int Combo;

    /// <summary>Total missed notes so far.</summary>
    public readonly int Misses;

    public ScoreUpdatePacket(
        int correlationId, float songTime, int score, int maxScore,
        float accuracy, Grade grade, float energy, int combo, int misses) {
        CorrelationId = correlationId;
        SongTime = songTime;
        Score = score;
        MaxScore = maxScore;
        Accuracy = accuracy;
        Grade = grade;
        Energy = energy;
        Combo = combo;
        Misses = misses;
    }

    /// <summary>
    /// Computes a <see cref="Grade"/> from an accuracy ratio (0–1).
    /// </summary>
    public static Grade GradeFromAccuracy(float accuracy) {
        if (accuracy >= 0.90f) return Grade.SS;
        if (accuracy >= 0.80f) return Grade.S;
        if (accuracy >= 0.65f) return Grade.A;
        if (accuracy >= 0.50f) return Grade.B;
        if (accuracy >= 0.35f) return Grade.C;
        if (accuracy >= 0.20f) return Grade.D;
        return Grade.E;
    }

    /// <summary>
    /// Serializes this packet to a fixed-size little-endian byte array.
    /// </summary>
    public byte[] ToBytes() {
        var buf = new byte[Size];
        var i = 0;
        WriteInt32(buf, ref i, CorrelationId);
        WriteSingle(buf, ref i, SongTime);
        WriteInt32(buf, ref i, Score);
        WriteInt32(buf, ref i, MaxScore);
        WriteSingle(buf, ref i, Accuracy);
        buf[i++] = (byte) Grade;
        WriteSingle(buf, ref i, Energy);
        WriteInt32(buf, ref i, Combo);
        WriteInt32(buf, ref i, Misses);
        return buf;
    }

    /// <summary>
    /// Deserializes a byte array into a <see cref="ScoreUpdatePacket"/>.
    /// </summary>
    /// <returns><see langword="false"/> if the payload is smaller than <see cref="Size"/>.</returns>
    public static bool TryParse(byte[] data, out ScoreUpdatePacket result) {
        if (data.Length < Size) {
            result = default;
            return false;
        }

        var i = 0;
        result = new ScoreUpdatePacket(
            ReadInt32(data, ref i),
            ReadSingle(data, ref i),
            ReadInt32(data, ref i),
            ReadInt32(data, ref i),
            ReadSingle(data, ref i),
            (Grade) data[i++],
            ReadSingle(data, ref i),
            ReadInt32(data, ref i),
            ReadInt32(data, ref i)
        );
        return true;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct FloatInt {
        [FieldOffset(0)] public float F;
        [FieldOffset(0)] public int I;
    }

    private static void WriteInt32(byte[] buf, ref int offset, int value) {
        buf[offset++] = (byte) value;
        buf[offset++] = (byte) (value >> 8);
        buf[offset++] = (byte) (value >> 16);
        buf[offset++] = (byte) (value >> 24);
    }

    private static void WriteSingle(byte[] buf, ref int offset, float value) {
        var fi = default(FloatInt);
        fi.F = value;
        WriteInt32(buf, ref offset, fi.I);
    }

    private static int ReadInt32(byte[] buf, ref int offset) {
        var value = buf[offset]
                  | (buf[offset + 1] << 8)
                  | (buf[offset + 2] << 16)
                  | (buf[offset + 3] << 24);
        offset += 4;
        return value;
    }

    private static float ReadSingle(byte[] buf, ref int offset) {
        var i = ReadInt32(buf, ref offset);
        var fi = default(FloatInt);
        fi.I = i;
        return fi.F;
    }
}
