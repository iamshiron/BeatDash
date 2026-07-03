using System;

namespace Shiron.BeatDash.Data.Realtime.Events;

/// <summary>
/// Notifies the web client that a device's online status changed.
/// </summary>
/// <param name="ClientId">The client identifier of the affected device.</param>
/// <param name="IsOnline">Whether the device is now online.</param>
/// <param name="Timestamp">When the status change occurred (UTC).</param>
public sealed record DeviceStatusEvent(Guid ClientId, bool IsOnline, DateTime Timestamp);
