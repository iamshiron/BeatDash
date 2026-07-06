using Quartz;

namespace Shiron.BeatDash.API.Services.BeatSaver;

/// <summary>
/// Quartz job that drives the BeatSaver fetch. With no job data it sweeps every
/// pending map; with a <see cref="BeatmapIdKey"/> it fetches that single map.
/// Non-concurrent so scheduled sweeps never overlap; ad-hoc single-map triggers
/// queue behind an in-flight sweep and run once it completes.
/// </summary>
[DisallowConcurrentExecution]
public sealed class BeatSaverFetchJob(
    IBeatSaverFetchService fetchService,
    ILogger<BeatSaverFetchJob> logger
) : IJob {
    /// <summary>Stable identity used by the schedule, startup and ad-hoc triggers.</summary>
    public static readonly JobKey Key = new("BeatSaverFetch");

    /// <summary>Job-data key holding the target beatmap id (single-map mode).</summary>
    public const string BeatmapIdKey = "beatmapId";

    /// <summary>Job-data key holding whether to force a re-download (single-map mode).</summary>
    public const string ForceKey = "force";

    public async Task Execute(IJobExecutionContext context) {
        var ct = context.CancellationToken;
        var data = context.MergedJobDataMap;

        if (data.ContainsKey(BeatmapIdKey) && Guid.TryParse(data.GetString(BeatmapIdKey), out var beatmapId)) {
            var force = data.ContainsKey(ForceKey) && data.GetBoolean(ForceKey);
            logger.LogInformation("BeatSaver job: single-map fetch {MapId} (force={Force})", beatmapId, force);
            await fetchService.FetchAsync(beatmapId, force, ct);
            return;
        }

        logger.LogInformation("BeatSaver job: sweeping pending maps");
        await fetchService.FetchPendingAsync(ct);
    }
}
