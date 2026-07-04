using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Shiron.BeatDash.Data.Socket;

namespace Shiron.BeatDash.Mod.Network;

/// <summary>
/// Reliable integrity channel. Accumulates discrete gameplay events into a
/// double-buffered batch and transmits them as a JSON <see cref="LiveStatsMessage"/>
/// over the TCP WebSocket. While one buffer is being sent, the other accumulates
/// new events, so a buffer being written to is never the one being transmitted
/// (and vice-versa). Connection-drop recovery (retransmit/retry) is deferred to V2.
/// </summary>
public sealed class StatAccumulatorService(
    NetworkManager networkManager,
    PluginConfig config
) {
    private readonly object _lock = new();
    private readonly int _bufferSize = Math.Max(1, config.TransmissionBufferSize);
    private readonly bool _doubleBuffering = !config.DisableDoubleBuffering;

    // Two physical buffers. In double-buffer mode the writer and sender always
    // target different slots (selected by _writeIndex). In single-buffer mode
    // only slot 0 is used as the live buffer; flushes send a detached copy.
    private readonly StatsBuffer[] _buffers = { new StatsBuffer(), new StatsBuffer() };
    private int _writeIndex;
    private bool _sending;

    /// <summary>The buffer currently accepting events (always accessed under <see cref="_lock"/>).</summary>
    private StatsBuffer WriteBuffer => _buffers[_writeIndex];

    /// <summary>Appends a per-note scoring event to the active write buffer.</summary>
    public void AddNoteEvent(NoteEventDto dto) {
        lock (_lock) {
            WriteBuffer.NoteEvents.Add(dto);
        }
    }

    /// <summary>Appends a combo-break event to the active write buffer.</summary>
    public void AddComboBreak(ComboBreakDto dto) {
        lock (_lock) {
            WriteBuffer.ComboBreaks.Add(dto);
        }
    }

    /// <summary>Appends an energy-change event to the active write buffer.</summary>
    public void AddEnergyChange(EnergyChangeDto dto) {
        lock (_lock) {
            WriteBuffer.EnergyChanges.Add(dto);
        }
    }

    /// <summary>Appends a score-delta event to the active write buffer.</summary>
    public void AddScoreChange(ScoreChangeDto dto) {
        lock (_lock) {
            WriteBuffer.ScoreChanges.Add(dto);
        }
    }

    /// <summary>
    /// True when the write buffer has reached the configured
    /// <see cref="PluginConfig.TransmissionBufferSize"/> threshold.
    /// </summary>
    public bool IsThresholdReached() {
        lock (_lock) {
            return WriteBuffer.TotalCount >= _bufferSize;
        }
    }

    /// <summary>
    /// Serializes and sends the accumulated events as a <see cref="LiveStatsMessage"/>
    /// over TCP. <paramref name="force"/> (level end) guarantees the final partial
    /// batch is delivered even if a previous flush is still in flight.
    /// </summary>
    public async Task FlushAsync(StatsSnapshot snapshot, int correlationId, bool force = false) {
        StatsBuffer? toSend;
        bool swapped;

        lock (_lock) {
            // The Begin* helpers assume the caller holds _lock.
            if (_doubleBuffering) {
                (toSend, swapped) = BeginDoubleBufferFlush(force);
            } else {
                (toSend, swapped) = BeginSingleBufferFlush();
            }
        }

        if (toSend == null) {
            return;
        }

        try {
            var message = BuildMessage(snapshot, correlationId, toSend);
            await networkManager.PostMessageAsync(JsonConvert.SerializeObject(message));
        } catch (Exception e) {
            Plugin.Log.Error($"Failed to flush live stats batch: {e.Message}");
        } finally {
            // Only the double-buffer swap path owns recycling the transmitted buffer;
            // the copy paths hand back a detached instance that is simply discarded.
            if (swapped) {
                lock (_lock) {
                    toSend.Clear();
                    _sending = false;
                }
            }
        }
    }

    /// <summary>
    /// Double-buffer flush: atomically swap the write index so the writer keeps
    /// appending to the other buffer while this one transmits. If a send is
    /// already in flight the flush is skipped (data stays buffered) unless
    /// <paramref name="force"/> snapshots the write buffer out for final delivery.
    /// </summary>
    private (StatsBuffer? Buffer, bool Swapped) BeginDoubleBufferFlush(bool force) {
        var write = WriteBuffer;
        if (write.IsEmpty) {
            return (null, false);
        }

        if (!_sending) {
            _sending = true;
            _writeIndex ^= 1; // writer now targets the other (free) buffer
            return (write, true);
        }

        // A previous flush is still transmitting; the other buffer is occupied.
        // Normally skip so we never write into a buffer that is being sent.
        if (!force) {
            return (null, false);
        }

        // Level-end must not lose data: snapshot the write buffer into a detached
        // copy and clear the original, leaving the in-flight buffer untouched.
        var snapshot = write.Clone();
        write.Clear();
        return (snapshot, false);
    }

    /// <summary>
    /// Single-buffer flush: snapshot the live buffer and clear it, so the writer
    /// resumes on the same (now empty) buffer while the detached copy transmits.
    /// </summary>
    private (StatsBuffer? Buffer, bool Swapped) BeginSingleBufferFlush() {
        var write = WriteBuffer;
        if (write.IsEmpty) {
            return (null, false);
        }

        var snapshot = write.Clone();
        write.Clear();
        return (snapshot, false);
    }

    private static LiveStatsMessage BuildMessage(StatsSnapshot snapshot, int correlationId, StatsBuffer buffer) {
        return new LiveStatsMessage {
            CorrelationId = correlationId,
            SongTime = snapshot.SongTime,
            Score = snapshot.Score,
            ModifiedScore = snapshot.ModifiedScore,
            MaxPossibleScore = snapshot.MaxPossibleScore,
            Energy = snapshot.Energy,
            CurrentCombo = snapshot.CurrentCombo,
            MaxCombo = snapshot.MaxCombo,
            LeftHand = snapshot.LeftHand,
            RightHand = snapshot.RightHand,
            NoteEvents = buffer.NoteEvents.ToArray(),
            ComboBreaks = buffer.ComboBreaks.ToArray(),
            EnergyChanges = buffer.EnergyChanges.ToArray(),
            ScoreChanges = buffer.ScoreChanges.ToArray()
        };
    }

    private sealed class StatsBuffer {
        public readonly List<NoteEventDto> NoteEvents = new List<NoteEventDto>();
        public readonly List<ComboBreakDto> ComboBreaks = new List<ComboBreakDto>();
        public readonly List<EnergyChangeDto> EnergyChanges = new List<EnergyChangeDto>();
        public readonly List<ScoreChangeDto> ScoreChanges = new List<ScoreChangeDto>();

        public int TotalCount =>
            NoteEvents.Count + ComboBreaks.Count + EnergyChanges.Count + ScoreChanges.Count;

        public bool IsEmpty => TotalCount == 0;

        public void Clear() {
            NoteEvents.Clear();
            ComboBreaks.Clear();
            EnergyChanges.Clear();
            ScoreChanges.Clear();
        }

        public StatsBuffer Clone() {
            var clone = new StatsBuffer();
            clone.NoteEvents.AddRange(NoteEvents);
            clone.ComboBreaks.AddRange(ComboBreaks);
            clone.EnergyChanges.AddRange(EnergyChanges);
            clone.ScoreChanges.AddRange(ScoreChanges);
            return clone;
        }
    }
}

/// <summary>
/// Point-in-time gameplay snapshot gathered at flush time and embedded in the
/// reliable <see cref="LiveStatsMessage"/> as an integrity anchor.
/// </summary>
public readonly struct StatsSnapshot {
    public readonly int SongTime;
    public readonly int Score;
    public readonly int ModifiedScore;
    public readonly int MaxPossibleScore;
    public readonly float Energy;
    public readonly int CurrentCombo;
    public readonly int MaxCombo;
    public readonly HandStatsDto LeftHand;
    public readonly HandStatsDto RightHand;

    public StatsSnapshot(
        int songTime,
        int score,
        int modifiedScore,
        int maxPossibleScore,
        float energy,
        int currentCombo,
        int maxCombo,
        HandStatsDto leftHand,
        HandStatsDto rightHand) {
        SongTime = songTime;
        Score = score;
        ModifiedScore = modifiedScore;
        MaxPossibleScore = maxPossibleScore;
        Energy = energy;
        CurrentCombo = currentCombo;
        MaxCombo = maxCombo;
        LeftHand = leftHand;
        RightHand = rightHand;
    }
}
