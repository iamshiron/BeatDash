using Microsoft.AspNetCore.SignalR;
using Shiron.BeatDash.Data.Realtime.Events;

namespace Shiron.BeatDash.API.Services.Realtime;

/// <summary>
/// Broadcasts real-time events to connected SignalR web clients via a typed hub context.
/// </summary>
public sealed class RealtimeBroadcaster(
    IHubContext<RealtimeHub, IRealtimeClient> hubContext
) : IRealtimeBroadcaster {
    /// <inheritdoc/>
    public Task SendDeviceStatusAsync(Guid userId, DeviceStatusEvent payload) {
        return hubContext.Clients
            .Group(RealtimeHub.GroupForUser(userId))
            .ReceiveDeviceStatus(payload);
    }

    /// <inheritdoc/>
    public Task SendDevicePairedAsync(Guid userId, DevicePairedEvent payload) {
        return hubContext.Clients
            .Group(RealtimeHub.GroupForUser(userId))
            .ReceiveDevicePaired(payload);
    }

    /// <inheritdoc/>
    public Task SendLiveMapStartedAsync(Guid userId, LiveMapStartedEvent payload) {
        return hubContext.Clients
            .Group(RealtimeHub.GroupForUser(userId))
            .ReceiveLiveMapStarted(payload);
    }

    /// <inheritdoc/>
    public Task SendLiveMapStateChangedAsync(Guid userId, LiveMapStateChangedEvent payload) {
        return hubContext.Clients
            .Group(RealtimeHub.GroupForUser(userId))
            .ReceiveLiveMapStateChanged(payload);
    }

    public Task SendLiveStatsAsync(Guid userId, LiveStatsEvent payload) {
        return hubContext.Clients
            .Group(RealtimeHub.GroupForUser(userId))
            .ReceiveLiveStats(payload);
    }

    /// <inheritdoc/>
    public Task SendScoreUpdateAsync(Guid userId, ScoreUpdateEvent payload) {
        return hubContext.Clients
            .Group(RealtimeHub.GroupForUser(userId))
            .ReceiveScoreUpdate(payload);
    }
}
