using System;
using Shiron.BeatDash.Data.Socket;

namespace Shiron.BeatDash.Data.Realtime.Events;

/// <summary>
/// Notifies the web client that a beatmap's gameplay state changed
/// (paused, resumed, finished, failed, or quit).
/// </summary>
/// <param name="MapId">The database ID of the beatmap, or null if not persisted.</param>
/// <param name="CorrelationId">The client-generated correlation ID linking to the map start event.</param>
/// <param name="State">The new gameplay state (see <see cref="MapState"/>).</param>
/// <param name="Results">Score/performance results, or null for pause/resume.</param>
/// <param name="Timestamp">When the state change occurred (UTC).</param>
public sealed record LiveMapStateChangedEvent(
    Guid? MapId,
    int CorrelationId,
    string State,
    MapResults? Results,
    DateTime Timestamp
);
