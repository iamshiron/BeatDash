using Quartz;

namespace Shiron.BeatDash.API.Services.BeatSaver;

/// <summary>
/// Fires the <see cref="BeatSaverFetchJob"/> on demand for a single beatmap — used
/// when a new map is added and by the admin refetch endpoint.
/// </summary>
public interface IBeatSaverFetchTrigger {
    /// <summary>
    /// Queues a single-map fetch through Quartz. Returns once the trigger is
    /// scheduled, not once the fetch completes.
    /// </summary>
    Task TriggerMapAsync(Guid beatmapId, bool force, CancellationToken ct);
}

/// <summary><see cref="ISchedulerFactory"/>-backed implementation.</summary>
public sealed class BeatSaverFetchTrigger(
    ISchedulerFactory schedulerFactory,
    ILogger<BeatSaverFetchTrigger> logger
) : IBeatSaverFetchTrigger {

    /// <inheritdoc/>
    public async Task TriggerMapAsync(Guid beatmapId, bool force, CancellationToken ct) {
        var scheduler = await schedulerFactory.GetScheduler(ct);
        var data = new JobDataMap {
            { BeatSaverFetchJob.BeatmapIdKey, beatmapId.ToString() },
            { BeatSaverFetchJob.ForceKey, force },
        };

        await scheduler.TriggerJob(BeatSaverFetchJob.Key, data, ct);
        logger.LogInformation("Queued BeatSaver fetch for map {MapId} (force={Force})", beatmapId, force);
    }
}
