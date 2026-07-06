using System;

namespace Shiron.BeatDash.Data.Realtime.Events;

/// <summary>
/// Broadcast to all web clients as the BeatSaver fetch/analysis pipeline drains its
/// backlog, so the UI can show live import progress. When <see cref="Pending"/> reaches
/// zero, processing is complete.
/// </summary>
/// <param name="Total">Total number of known beatmaps.</param>
/// <param name="Processed">Beatmaps that are no longer pending (fetched or terminal).</param>
/// <param name="Pending">Beatmaps still waiting to be fetched/processed.</param>
/// <param name="Timestamp">When the snapshot was taken (UTC).</param>
public sealed record MapProcessingEvent(int Total, int Processed, int Pending, DateTime Timestamp);
