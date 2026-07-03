using Microsoft.AspNetCore.SignalR;
using Shiron.BeatDash.Data.Realtime.Events;

namespace Shiron.BeatDash.API.Services.Realtime;

/// <summary>
/// Provides a clean, type-safe API for pushing real-time events to connected web clients.
/// Inject this into any service or endpoint that needs to broadcast to the frontend.
/// </summary>
public interface IRealtimeBroadcaster {
    /// <summary>
    /// Notifies a user's web clients that a device's online status changed.
    /// </summary>
    /// <param name="userId">The user whose clients should receive the event.</param>
    /// <param name="payload">The device status event details.</param>
    Task SendDeviceStatusAsync(Guid userId, DeviceStatusEvent payload);

    /// <summary>
    /// Notifies a user's web clients that a device was successfully paired.
    /// </summary>
    /// <param name="userId">The user whose clients should receive the event.</param>
    /// <param name="payload">The device paired event details.</param>
    Task SendDevicePairedAsync(Guid userId, DevicePairedEvent payload);

    /// <summary>
    /// Notifies a user's web clients that the user started playing a beatmap.
    /// </summary>
    /// <param name="userId">The user whose clients should receive the event.</param>
    /// <param name="payload">The live map started event details.</param>
    Task SendLiveMapStartedAsync(Guid userId, LiveMapStartedEvent payload);

    /// <summary>
    /// Notifies a user's web clients that a beatmap's gameplay state changed.
    /// </summary>
    /// <param name="userId">The user whose clients should receive the event.</param>
    /// <param name="payload">The live map state changed event details.</param>
    Task SendLiveMapStateChangedAsync(Guid userId, LiveMapStateChangedEvent payload);

    Task SendLiveStatsAsync(Guid userId, LiveStatsEvent payload);

    /// <summary>
    /// Notifies a user's web clients that a score-relevant event occurred during gameplay.
    /// </summary>
    Task SendScoreUpdateAsync(Guid userId, ScoreUpdateEvent payload);
}
