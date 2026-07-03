using System;

namespace Shiron.BeatDash.Data.Realtime.Events;

/// <summary>
/// Notifies the web client that a score-relevant event occurred (note scored, missed, etc.).
/// Contains the essential values for real-time dashboard animations.
/// </summary>
/// <param name="CorrelationId">Links to the original map start event.</param>
/// <param name="SongTime">Position in the original song timeline (seconds).</param>
/// <param name="Score">Current cumulative modified score.</param>
/// <param name="MaxScore">Maximum possible modified score at this point.</param>
/// <param name="Accuracy">Accuracy ratio (0–1).</param>
/// <param name="Rank">Letter grade: SS, S, A, B, C, D, E.</param>
/// <param name="Energy">Saber energy / health (0–1).</param>
/// <param name="Combo">Current active combo.</param>
/// <param name="Timestamp">When the event was received by the server (UTC).</param>
public sealed record ScoreUpdateEvent(
    int CorrelationId,
    float SongTime,
    int Score,
    int MaxScore,
    float Accuracy,
    string Rank,
    float Energy,
    int Combo,
    int Misses,
    DateTime Timestamp
);
