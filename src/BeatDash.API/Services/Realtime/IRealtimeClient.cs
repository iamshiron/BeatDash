using Shiron.BeatDash.Data.Realtime.Events;

namespace Shiron.BeatDash.API.Services.Realtime;

/// <summary>
/// Defines the methods the backend can invoke on connected SignalR web clients.
/// Used as the type parameter for <see cref="Microsoft.AspNetCore.SignalR.Hub{T}"/>
/// to provide compile-time safety for server-to-client calls.
/// </summary>
public interface IRealtimeClient {
    /// <summary>
    /// Notifies the client that a device's online status changed.
    /// </summary>
    /// <param name="payload">The device status event details.</param>
    Task ReceiveDeviceStatus(DeviceStatusEvent payload);

    /// <summary>
    /// Notifies the client that a device was successfully paired.
    /// </summary>
    /// <param name="payload">The device paired event details.</param>
    Task ReceiveDevicePaired(DevicePairedEvent payload);

    /// <summary>
    /// Notifies the client that the user started playing a beatmap.
    /// </summary>
    /// <param name="payload">The live map started event details.</param>
    Task ReceiveLiveMapStarted(LiveMapStartedEvent payload);
}
