using Microsoft.EntityFrameworkCore;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.API.Services;

/// <summary>
/// One-shot startup backfill that folds any finished, non-auto session not yet
/// reflected in the note aggregate (<see cref="PlaySession.AggregatedAt"/> null).
/// Idempotent — each fold atomically claims its session — so re-running on every
/// startup is safe and only ever processes the outstanding tail.
/// </summary>
public sealed class WeaknessBackfillService(
    IServiceScopeFactory scopeFactory,
    ILogger<WeaknessBackfillService> logger
) : BackgroundService {
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        try {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<BeatDashDbContext>();
            var aggregation = scope.ServiceProvider.GetRequiredService<IWeaknessAggregationService>();

            var pending = await db.PlaySessions
                .AsNoTracking()
                .Where(s =>
                    s.AggregatedAt == null &&
                    !s.AutoMode &&
                    s.EndReason == PlaySessionEndReason.Finished &&
                    s.Results != null)
                .Select(s => s.Id)
                .ToListAsync(stoppingToken);

            if (pending.Count == 0) return;

            logger.LogInformation("Backfilling weakness aggregate for {Count} sessions", pending.Count);
            foreach (var id in pending) {
                stoppingToken.ThrowIfCancellationRequested();
                await aggregation.FoldSessionAsync(id, stoppingToken);
            }
            logger.LogInformation("Weakness aggregate backfill complete");
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            logger.LogError(ex, "Weakness aggregate backfill failed");
        }
    }
}
