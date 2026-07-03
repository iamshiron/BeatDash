using System;
using Shiron.BeatDash.Data.Socket;

namespace Shiron.BeatDash.Data.Realtime.Events;

/// <summary>
/// Notifies the web client of a periodic live gameplay statistics update.
/// Contains cumulative score, energy, combo, and per-hand breakdown data
/// at a specific point in the song timeline.
/// </summary>
/// <param name="CorrelationId">Links to the original map start event.</param>
/// <param name="SongTime">Position in original song timeline (seconds). Pauses stop advancement.</param>
/// <param name="Score">Cumulative multiplied score.</param>
/// <param name="ModifiedScore">Cumulative modified score (after modifier multipliers).</param>
/// <param name="MaxPossibleScore">Maximum possible multiplied score at this point.</param>
/// <param name="Energy">Saber energy / health (0–1).</param>
/// <param name="MaxCombo">Highest combo achieved so far.</param>
/// <param name="LeftHand">Cumulative stats for the left hand (ColorA).</param>
/// <param name="RightHand">Cumulative stats for the right hand (ColorB).</param>
/// <param name="Timestamp">When the update was received by the server (UTC).</param>
public sealed record LiveStatsEvent(
    int CorrelationId,
    float SongTime,
    int Score,
    int ModifiedScore,
    int MaxPossibleScore,
    float Energy,
    int CurrentCombo,
    int MaxCombo,
    HandStatsDto LeftHand,
    HandStatsDto RightHand,
    NoteEventDto[] NoteEvents,
    ComboBreakDto[] ComboBreaks,
    EnergyChangeDto[] EnergyChanges,
    DateTime Timestamp
);
