using Quartz;

namespace Shiron.BeatDash.API.Services.BeatmapAnalysis;

/// <summary>
/// Quartz job that re-scores stored analyses whose metric calibration is stale (see
/// <see cref="IBeatmapAnalysisService.RescoreStaleAsync"/>). Fired once at startup so a
/// calibration change picked up from config triggers a queued, background recompute of
/// every affected map — not blocking boot, and a no-op when nothing changed.
/// Non-concurrent so overlapping runs never fight.
/// </summary>
[DisallowConcurrentExecution]
public sealed class BeatmapRescoreJob(
    IBeatmapAnalysisService analysis,
    ILogger<BeatmapRescoreJob> logger
) : IJob {
    public static readonly JobKey Key = new("BeatmapRescore");

    public async Task Execute(IJobExecutionContext context) {
        logger.LogInformation("Rescore job: checking for metrics stale against the current calibration");
        await analysis.RescoreStaleAsync(context.CancellationToken);
    }
}
