using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.Data.Socket;

namespace Shiron.BeatDash.API.Services.Socket.Handlers;

/// <summary>
/// Handles binary <see cref="BinaryPacketTypes.MotionFrameBatch"/> packets received from the client.
/// Frames are forwarded to <see cref="IMotionFrameBuffer"/>, which decimates to the target rate and
/// stores them for replay. They are compressed and persisted once the play ends.
/// </summary>
public sealed class MotionFrameHandler(
    ILogger<MotionFrameHandler> logger,
    IMotionFrameBuffer buffer,
    IOptions<MotionFrameOptions> options
) : ISocketBinaryHandler {
    private readonly int _maxFramesPerPacket = options.Value.MaxFramesPerPacket;

    public BinaryPacketTypes PacketType => BinaryPacketTypes.MotionFrameBatch;

    public Task HandleAsync(SocketContext context, ReadOnlyMemory<byte> data, CancellationToken ct) {
        if (data.Length < 6) {
            logger.LogWarning("Motion frame packet too small ({Bytes} bytes)", data.Length);
            return Task.CompletedTask;
        }

        var span = data.Span;
        var correlationId = BinaryPrimitives.ReadInt32LittleEndian(span[..4]);
        var frameCount = BinaryPrimitives.ReadInt16LittleEndian(span.Slice(4, 2));

        if (frameCount <= 0) return Task.CompletedTask;

        if (frameCount > _maxFramesPerPacket) {
            logger.LogWarning(
                "Dropping motion frame batch: {Count} frames exceeds max {Max} (corr={CorrelationId})",
                frameCount, _maxFramesPerPacket, correlationId);
            return Task.CompletedTask;
        }

        var expectedSize = 6 + MotionFrame.Size * frameCount;
        if (data.Length < expectedSize) {
            logger.LogWarning("Motion frame packet truncated: expected {Expected} bytes, got {Actual} (corr={CorrelationId})",
                expectedSize, data.Length, correlationId);
            return Task.CompletedTask;
        }

        var frameSpan = MemoryMarshal.Cast<byte, MotionFrame>(span.Slice(6, MotionFrame.Size * frameCount));
        buffer.Append(context.SessionId, correlationId, frameSpan);

        logger.LogDebug("Buffered motion frames: {Count} frames (corr={CorrelationId})",
            frameCount, correlationId);

        return Task.CompletedTask;
    }
}
