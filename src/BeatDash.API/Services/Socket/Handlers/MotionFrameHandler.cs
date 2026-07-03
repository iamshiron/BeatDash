using System.Buffers.Binary;
using Microsoft.Extensions.Logging;
using Shiron.BeatDash.Data.Socket;

namespace Shiron.BeatDash.API.Services.Socket.Handlers;

/// <summary>
/// Handles binary <see cref="BinaryPacketTypes.MotionFrameBatch"/> packets received from the client.
/// Stores saber/head motion data for future replay functionality.
/// </summary>
public sealed class MotionFrameHandler(
    ILogger<MotionFrameHandler> logger
) : ISocketBinaryHandler {
    public BinaryPacketTypes PacketType => BinaryPacketTypes.MotionFrameBatch;

    public Task HandleAsync(SocketContext context, ReadOnlyMemory<byte> data, CancellationToken ct) {
        if (data.Length < 6) {
            logger.LogWarning("Motion frame packet too small ({Bytes} bytes)", data.Length);
            return Task.CompletedTask;
        }

        var span = data.Span;
        var correlationId = BinaryPrimitives.ReadInt32LittleEndian(span[..4]);
        var frameCount = BinaryPrimitives.ReadInt16LittleEndian(span.Slice(4, 2));

        var expectedSize = 6 + MotionFrame.Size * frameCount;
        if (data.Length < expectedSize) {
            logger.LogWarning("Motion frame packet truncated: expected {Expected} bytes, got {Actual} (corr={CorrelationId})",
                expectedSize, data.Length, correlationId);
            return Task.CompletedTask;
        }

        logger.LogDebug("Received motion frames: {Count} frames, {Bytes} bytes (corr={CorrelationId})",
            frameCount, data.Length, correlationId);

        return Task.CompletedTask;
    }
}
