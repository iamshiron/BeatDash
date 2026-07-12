using System.IO.Compression;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.API.Services.Motion;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.API.Services.Socket;

/// <summary>
/// Finalizes a play's saber motion data: drains the accumulated buffer,
/// Brotli-compresses the samples on a background task, and persists a single
/// <see cref="PlaySessionItemMotionFrame"/> row. Safe to invoke fire-and-forget
/// — all failures are logged and swallowed.
/// </summary>
public interface IMotionFramePersistence {
    /// <summary>
    /// Compresses and stores the buffered motion frames for the given play
    /// session. No-op if nothing was buffered.
    /// </summary>
    Task PersistAsync(Guid sessionId, int correlationId, Guid playSessionId, CancellationToken ct);
}

/// <summary>
/// <see cref="IDbContextFactory{TContext}"/>-backed implementation. Registered
/// as a singleton so it has no dependency on a request scope.
/// </summary>
public sealed class MotionFramePersistence(
    IMotionFrameBuffer buffer,
    IDbContextFactory<BeatDashDbContext> dbFactory,
    IOptions<MotionFrameOptions> options,
    ILogger<MotionFramePersistence> logger
) : IMotionFramePersistence {
    private readonly int _sampleRateHz = Math.Max(1, options.Value.TargetHz);

    /// <inheritdoc/>
    public async Task PersistAsync(Guid sessionId, int correlationId, Guid playSessionId, CancellationToken ct) {
        try {
            var snapshot = buffer.Take(sessionId, correlationId);
            if (snapshot is null) {
                logger.LogDebug("No motion frames to persist (corr={CorrelationId})", correlationId);
                return;
            }

            var compressed = await Task.Run(() => Compress(snapshot.Samples), ct);

            // Derive scalar motion metrics from the raw samples while they're still
            // in memory, so the dashboard never has to re-decompress the blob.
            var summary = MotionSummaryCalculator.Compute(
                snapshot.Samples, snapshot.FrameCount, _sampleRateHz, snapshot.LastSongTimeMs, playSessionId);

            await using var db = await dbFactory.CreateDbContextAsync(ct);
            db.PlaySessionItemMotionFrames.Add(new PlaySessionItemMotionFrame {
                PlaySessionId = playSessionId,
                CorrelationId = correlationId,
                SongTimeMs = snapshot.LastSongTimeMs,
                FrameCount = snapshot.FrameCount,
                Data = compressed,
            });
            db.PlaySessionMotionSummaries.Add(summary);
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "Persisted {Frames} motion frames ({Bytes} bytes compressed) for play session {PlaySessionId}",
                snapshot.FrameCount, compressed.Length, playSessionId);
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            logger.LogError(ex,
                "Failed to persist motion frames (session={SessionId}, corr={CorrelationId})",
                sessionId, correlationId);
        }
    }

    /// <summary>
    /// Brotli-compresses the flattened saber samples. The float layout is
    /// preserved verbatim (left then right saber, 7 floats each).
    /// </summary>
    private static byte[] Compress(float[] samples) {
        var raw = MemoryMarshal.AsBytes<float>((ReadOnlySpan<float>) samples);
        using var output = new MemoryStream(raw.Length);
        using (var brotli = new BrotliStream(output, CompressionLevel.Optimal, leaveOpen: true)) {
            brotli.Write(raw);
        }
        return output.ToArray();
    }
}
