using System.Collections.Concurrent;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.Data.Socket;
using Microsoft.Extensions.Options;

namespace Shiron.BeatDash.API.Services.Socket;

/// <summary>
/// Accumulates motion frames per active play, decimating to a fixed
/// sample rate. Frames are stored in a fixed layout — left saber, right saber,
/// then head, each <c>PosX, PosY, PosZ, RotX, RotY, RotZ, RotW</c> — so the
/// serialized blob always has the same structure.
/// </summary>
public interface IMotionFrameBuffer {
    /// <summary>
    /// Appends a batch of frames. Applies the per-play flood guard, then slots
    /// each frame into its target-rate bucket (last frame in a bucket wins).
    /// </summary>
    void Append(Guid sessionId, int correlationId, ReadOnlySpan<MotionFrame> frames);

    /// <summary>
    /// Removes and returns the accumulated samples for a play, or <c>null</c>
    /// if nothing was collected. Called once when the play ends.
    /// </summary>
    MotionFrameSnapshot? Take(Guid sessionId, int correlationId);
}

/// <summary>
/// The drained contents of a play's motion buffer.
/// </summary>
/// <param name="Samples">Flattened samples: <paramref name="FrameCount"/> * 21 floats.</param>
/// <param name="FrameCount">Number of stored frames.</param>
/// <param name="LastSongTimeMs">Song time (ms) of the newest stored frame.</param>
public sealed record MotionFrameSnapshot(float[] Samples, int FrameCount, int LastSongTimeMs);

/// <summary>
/// <see cref="ConcurrentDictionary{TKey, TValue}"/>-backed implementation. Each
/// play gets its own accumulator keyed by socket session and correlation ID.
/// </summary>
public sealed class MotionFrameBuffer(IOptions<MotionFrameOptions> options) : IMotionFrameBuffer {
    /// <summary>7 floats (Pos, Rot) per transform * 3 (left saber, right saber, head).</summary>
    private const int FloatsPerFrame = 21;

    private readonly MotionFrameOptions _options = options.Value;
    private readonly ConcurrentDictionary<(Guid SessionId, int CorrelationId), Accumulator> _buffers = new();

    /// <inheritdoc/>
    public void Append(Guid sessionId, int correlationId, ReadOnlySpan<MotionFrame> frames) {
        if (frames.IsEmpty) return;
        var acc = _buffers.GetOrAdd((sessionId, correlationId), _ => new Accumulator(_options));
        acc.Append(frames);
    }

    /// <inheritdoc/>
    public MotionFrameSnapshot? Take(Guid sessionId, int correlationId) {
        if (!_buffers.TryRemove((sessionId, correlationId), out var acc)) return null;
        return acc.Drain();
    }

    /// <summary>
    /// Per-play accumulator. Buckets frames by integer song-time slot so several
    /// frames landing in the same slot collapse to the newest one (minimizing
    /// replay jitter). Slot keys arrive in ascending order, so draining is a
    /// straight range walk — no sort.
    /// </summary>
    private sealed class Accumulator(MotionFrameOptions options) {
        private readonly Dictionary<int, MotionFrame> _frames = new();
        private readonly int _targetHz = Math.Max(1, options.TargetHz);
        private readonly long _minGapMs = Math.Max(0, options.MinPacketGapMs);
        private readonly Lock _gate = new();

        private long _lastAcceptedAtMs;
        private int _minSlot = int.MaxValue;
        private int _maxSlot = int.MinValue;

        public void Append(ReadOnlySpan<MotionFrame> frames) {
            lock (_gate) {
                // Flood guard: drop the whole batch if it arrived too soon.
                var now = Environment.TickCount64;
                if (now - _lastAcceptedAtMs < _minGapMs) return;
                _lastAcceptedAtMs = now;

                for (var i = 0; i < frames.Length; i++) {
                    ref readonly var f = ref frames[i];
                    var slot = (f.SongTime * _targetHz) / 1000;
                    _frames[slot] = f; // last frame in the slot wins
                    if (slot < _minSlot) _minSlot = slot;
                    if (slot > _maxSlot) _maxSlot = slot;
                }
            }
        }

        public MotionFrameSnapshot? Drain() {
            lock (_gate) {
                if (_frames.Count == 0) return null;

                var frameCount = _frames.Count;
                var lastSongTimeMs = _frames[_maxSlot].SongTime;
                var samples = new float[frameCount * FloatsPerFrame];

                var w = 0;
                for (var slot = _minSlot; slot <= _maxSlot; slot++) {
                    if (!_frames.TryGetValue(slot, out var f)) continue;
                    var l = f.LeftSaber;
                    var r = f.RightSaber;
                    samples[w++] = l.PosX; samples[w++] = l.PosY; samples[w++] = l.PosZ;
                    samples[w++] = l.RotX; samples[w++] = l.RotY; samples[w++] = l.RotZ; samples[w++] = l.RotW;
                    samples[w++] = r.PosX; samples[w++] = r.PosY; samples[w++] = r.PosZ;
                    samples[w++] = r.RotX; samples[w++] = r.RotY; samples[w++] = r.RotZ; samples[w++] = r.RotW;
                    var h = f.Head;
                    samples[w++] = h.PosX; samples[w++] = h.PosY; samples[w++] = h.PosZ;
                    samples[w++] = h.RotX; samples[w++] = h.RotY; samples[w++] = h.RotZ; samples[w++] = h.RotW;
                }

                _frames.Clear();
                return new MotionFrameSnapshot(samples, frameCount, lastSongTimeMs);
            }
        }
    }
}
