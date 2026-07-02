using System;

namespace Shiron.BeatDash.Data.Socket;

/// <summary>
/// Builds and parses the cover-image binary payload, which prefixes raw PNG
/// bytes with a 4-byte little-endian correlation ID. The ID lets the server
/// match an incoming cover image back to its <see cref="MapStartMessage"/>.
/// </summary>
public static class MapCoverImagePacket {
    /// <summary>
    /// Size of the correlation ID prefix, in bytes.
    /// </summary>
    public const int CorrelationIdSize = 4;

    /// <summary>
    /// Builds a cover-image payload from a correlation ID and PNG bytes.
    /// </summary>
    public static byte[] Build(int correlationId, byte[] png) {
        var result = new byte[CorrelationIdSize + png.Length];
        result[0] = (byte) correlationId;
        result[1] = (byte) (correlationId >> 8);
        result[2] = (byte) (correlationId >> 16);
        result[3] = (byte) (correlationId >> 24);
        png.CopyTo(result, CorrelationIdSize);
        return result;
    }

    /// <summary>
    /// Splits a cover-image payload into its correlation ID and PNG bytes.
    /// </summary>
    /// <returns><see langword="false"/> if the payload is too small to contain the prefix.</returns>
    public static bool TryParse(byte[] data, out int correlationId, out byte[] png) {
        if (data.Length < CorrelationIdSize) {
            correlationId = 0;
            png = Array.Empty<byte>();
            return false;
        }

        correlationId = data[0] | (data[1] << 8) | (data[2] << 16) | (data[3] << 24);
        png = new byte[data.Length - CorrelationIdSize];
        Buffer.BlockCopy(data, CorrelationIdSize, png, 0, png.Length);
        return true;
    }
}
