using System;

namespace Shiron.BeatDash.Data.Realtime.Events;

/// <summary>
/// Notifies the web client that a device was successfully paired
/// (PIN consumed and authenticated).
/// </summary>
/// <param name="ClientId">The client identifier of the newly paired device.</param>
/// <param name="Name">The display name of the device.</param>
/// <param name="Timestamp">When the pairing occurred (UTC).</param>
public sealed record DevicePairedEvent(Guid ClientId, string Name, DateTime Timestamp);
