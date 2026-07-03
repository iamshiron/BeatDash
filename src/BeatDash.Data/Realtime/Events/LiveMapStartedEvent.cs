using System;
using Shiron.BeatDash.Data.Socket;

namespace Shiron.BeatDash.Data.Realtime.Events;

/// <summary>
/// Notifies the web client that the user started playing a beatmap.
/// Contains the map metadata relevant for a live dashboard view.
/// </summary>
/// <param name="MapId">The database ID of the beatmap, or null if not yet persisted.</param>
/// <param name="SongName">The display name of the song.</param>
/// <param name="SongSubName">The subtitle of the song, if any.</param>
/// <param name="SongAuthor">The song artist.</param>
/// <param name="Mapper">The beatmap mapper(s).</param>
/// <param name="Bpm">The beats per minute.</param>
/// <param name="DurationMs">The song duration in milliseconds.</param>
/// <param name="Difficulty">The serialized difficulty rank (e.g. "Expert").</param>
/// <param name="DifficultyName">The display difficulty name (may be a custom label).</param>
/// <param name="NotesPerSecond">Average cuttable notes per second.</param>
/// <param name="NoteJumpSpeed">The note jump speed, or null if unset.</param>
/// <param name="BombCount">Total bombs in the map.</param>
/// <param name="ObstacleCount">Total walls/obstacles.</param>
/// <param name="CuttableObjectCount">Total cuttable notes.</param>
/// <param name="LaneCount">Number of lanes.</param>
/// <param name="Characteristic">The beatmap characteristic serialized name (e.g. "Standard").</param>
/// <param name="ModifierFlags">Packed bitmask of gameplay modifiers. See <see cref="Shiron.BeatDash.Data.Socket.ModifierBit"/>.</param>
/// <param name="SongSpeed">Actual playback speed multiplier (e.g. 1.0, 1.5).</param>
/// <param name="NotesPerHandLeft">Total ColorA (left saber) note count.</param>
/// <param name="NotesPerHandRight">Total ColorB (right saber) note count.</param>
/// <param name="NpsCurve">Notes per second, one int per second of song time.</param>
/// <param name="WallTimeline">All walls/obstacles with start time, duration, and position.</param>
/// <param name="BombPositions">All bomb positions in the song timeline.</param>
/// <param name="Timestamp">When the event occurred (UTC).</param>
public sealed record LiveMapStartedEvent(
    Guid? MapId,
    string SongName,
    string SongSubName,
    string SongAuthor,
    string Mapper,
    float Bpm,
    int DurationMs,
    string Difficulty,
    string DifficultyName,
    float NotesPerSecond,
    float? NoteJumpSpeed,
    int BombCount,
    int ObstacleCount,
    int CuttableObjectCount,
    int LaneCount,
    string Characteristic,
    int ModifierFlags,
    float SongSpeed,
    int NotesPerHandLeft,
    int NotesPerHandRight,
    int[] NpsCurve,
    WallEntryDto[] WallTimeline,
    BombEntryDto[] BombPositions,
    DateTime Timestamp
);
