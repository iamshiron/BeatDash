using System.IO.Compression;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.DB;

namespace Shiron.BeatDash.API.Services.Motion;

/// <summary>
/// One-shot startup backfill that computes a <c>PlaySessionMotionSummary</c> for
/// plays that have a stored motion blob but no summary yet. This is the only path
/// that decompresses the blob for analytics — live plays compute the summary inline
/// from the in-memory samples.
/// </summary>
public sealed class MotionSummaryBackfillService(
    IServiceScopeFactory scopeFactory,
    IOptions<MotionFrameOptions> options,
    ILogger<MotionSummaryBackfillService> logger
) : BackgroundService {
    private readonly int _sampleRateHz = Math.Max(1, options.Value.TargetHz);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        try {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<BeatDashDbContext>();

            // Motion frames whose play session has no summary row yet.
            var pending = await db.PlaySessionItemMotionFrames
                .AsNoTracking()
                .Where(f => !db.PlaySessionMotionSummaries.Any(m => m.PlaySessionId == f.PlaySessionId))
                .Select(f => new { f.PlaySessionId, f.FrameCount, f.SongTimeMs, f.Data })
                .ToListAsync(stoppingToken);

            if (pending.Count == 0) return;

            logger.LogInformation("Backfilling motion summaries for {Count} plays", pending.Count);
            foreach (var frame in pending) {
                stoppingToken.ThrowIfCancellationRequested();
                var samples = Decompress(frame.Data);
                var summary = MotionSummaryCalculator.Compute(
                    samples, frame.FrameCount, _sampleRateHz, frame.SongTimeMs, frame.PlaySessionId);
                db.PlaySessionMotionSummaries.Add(summary);
            }
            await db.SaveChangesAsync(stoppingToken);
            logger.LogInformation("Motion summary backfill complete");
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            logger.LogError(ex, "Motion summary backfill failed");
        }
    }

    /// <summary>Reverses <c>MotionFramePersistence.Compress</c>: Brotli → float samples.</summary>
    private static float[] Decompress(byte[] compressed) {
        using var input = new MemoryStream(compressed);
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        brotli.CopyTo(output);
        var bytes = output.ToArray();
        return MemoryMarshal.Cast<byte, float>(bytes).ToArray();
    }
}
