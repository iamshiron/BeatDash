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
}
