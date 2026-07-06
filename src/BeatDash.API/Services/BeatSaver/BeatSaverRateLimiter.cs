using System.Diagnostics;
using Microsoft.Extensions.Options;
using Shiron.BeatDash.API.Configuration;

namespace Shiron.BeatDash.API.Services.BeatSaver;

/// <summary>
/// Process-wide throttle for outbound BeatSaver requests. Spaces calls so no more
/// than <see cref="BeatSaverOptions.RequestsPerMinute"/> are issued per minute,
/// shared across the scheduled sweep and on-new-map triggers. Registered as a
/// singleton.
/// </summary>
public sealed class BeatSaverRateLimiter {
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly double _minIntervalMs;
    private long _lastTimestamp;

    public BeatSaverRateLimiter(IOptions<BeatSaverOptions> options) {
        var rpm = Math.Max(1, options.Value.RequestsPerMinute);
        _minIntervalMs = 60_000.0 / rpm;
    }

    /// <summary>
    /// Blocks until the caller is allowed to issue its next request, then records
    /// the moment as the new baseline. Serializes callers so spacing is honored.
    /// </summary>
    public async Task WaitAsync(CancellationToken ct) {
        await _gate.WaitAsync(ct);
        try {
            if (_lastTimestamp != 0) {
                var elapsedMs = Stopwatch.GetElapsedTime(_lastTimestamp).TotalMilliseconds;
                var remainingMs = _minIntervalMs - elapsedMs;
                if (remainingMs > 0) {
                    await Task.Delay(TimeSpan.FromMilliseconds(remainingMs), ct);
                }
            }

            _lastTimestamp = Stopwatch.GetTimestamp();
        } finally {
            _gate.Release();
        }
    }
}
