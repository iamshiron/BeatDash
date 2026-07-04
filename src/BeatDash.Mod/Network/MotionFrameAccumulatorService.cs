using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shiron.BeatDash.Data.Socket;

namespace Shiron.BeatDash.Mod.Network;

/// <summary>
/// Reliable channel for saber/head motion frames. Accumulates frames into a
/// double-buffered batch (mirroring <see cref="StatAccumulatorService"/>) and
/// transmits them as a binary <see cref="BinaryPacketTypes.MotionFrameBatch"/>
/// packet. While one buffer is being sent, the other accumulates, so a buffer
/// being written to is never the one being transmitted. Frames are persisted on
/// the server, so the batch is always forced over TCP — UDP is never used here
/// (the NetworkManager UDP whitelist permits only ephemeral packet types).
/// Connection-drop recovery (retransmit/retry) is deferred to V2.
/// </summary>
public sealed class MotionFrameAccumulatorService(
    NetworkManager networkManager,
    PluginConfig config
) {
    private readonly object _lock = new();
    private readonly int _bufferSize = Math.Max(1, config.MotionFrameBufferSize);
    private readonly bool _doubleBuffering = !config.DisableDoubleBuffering;

    // Two physical buffers. In double-buffer mode the writer and sender always
    // target different slots (selected by _writeIndex). In single-buffer mode
    // only slot 0 is used as the live buffer; flushes send a detached copy.
    private readonly List<MotionFrame>[] _buffers = { new List<MotionFrame>(), new List<MotionFrame>() };
    private int _writeIndex;
    private bool _sending;

    /// <summary>The buffer currently accepting frames (always accessed under <see cref="_lock"/>).</summary>
    private List<MotionFrame> WriteBuffer => _buffers[_writeIndex];

    /// <summary>Appends a sampled motion frame to the active write buffer.</summary>
    public void Append(MotionFrame frame) {
        lock (_lock) {
            WriteBuffer.Add(frame);
        }
    }

    /// <summary>
    /// True when the write buffer has reached the configured
    /// <see cref="PluginConfig.MotionFrameBufferSize"/> threshold.
    /// </summary>
    public bool IsThresholdReached() {
        lock (_lock) {
            return WriteBuffer.Count >= _bufferSize;
        }
    }

    /// <summary>
    /// Packs and sends the accumulated frames as a
    /// <see cref="BinaryPacketTypes.MotionFrameBatch"/> packet.
    /// <paramref name="force"/> (level end) guarantees the final partial batch is
    /// delivered even if a previous flush is still in flight.
    /// </summary>
    public async Task FlushAsync(int correlationId, bool force = false) {
        List<MotionFrame>? toSend;
        bool swapped;

        lock (_lock) {
            // The Begin* helpers assume the caller holds _lock.
            if (_doubleBuffering) {
                (toSend, swapped) = BeginDoubleBufferFlush(force);
            } else {
                (toSend, swapped) = BeginSingleBufferFlush();
            }
        }

        if (toSend == null || toSend.Count == 0) {
            return;
        }

        try {
            var payload = PackMotionFrames(correlationId, toSend);
            await networkManager.PostBinaryAsync(BinaryPacketTypes.MotionFrameBatch, payload, forceTcp: true);
        } catch (Exception e) {
            Plugin.Log.Error($"Failed to flush motion frames batch: {e.Message}");
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
    private (List<MotionFrame>? Buffer, bool Swapped) BeginDoubleBufferFlush(bool force) {
        var write = WriteBuffer;
        if (write.Count == 0) {
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
        var snapshot = new List<MotionFrame>(write);
        write.Clear();
        return (snapshot, false);
    }

    /// <summary>
    /// Single-buffer flush: snapshot the live buffer and clear it, so the writer
    /// resumes on the same (now empty) buffer while the detached copy transmits.
    /// </summary>
    private (List<MotionFrame>? Buffer, bool Swapped) BeginSingleBufferFlush() {
        var write = WriteBuffer;
        if (write.Count == 0) {
            return (null, false);
        }

        var snapshot = new List<MotionFrame>(write);
        write.Clear();
        return (snapshot, false);
    }

    private static unsafe byte[] PackMotionFrames(int correlationId, List<MotionFrame> frames) {
        var buffer = new byte[6 + MotionFrame.Size * frames.Count];
        fixed (byte* pBuf = buffer) {
            *(int*) pBuf = correlationId;
            *(short*) (pBuf + 4) = (short) frames.Count;

            var offset = 6;
            for (var i = 0; i < frames.Count; i++) {
                var f = frames[i];
                var framePtr = pBuf + offset;
                *(int*) framePtr = f.SongTime;
                var p = (float*) (framePtr + 4);
                *p++ = f.LeftSaber.PosX;
                *p++ = f.LeftSaber.PosY;
                *p++ = f.LeftSaber.PosZ;
                *p++ = f.LeftSaber.RotX;
                *p++ = f.LeftSaber.RotY;
                *p++ = f.LeftSaber.RotZ;
                *p++ = f.LeftSaber.RotW;
                *p++ = f.RightSaber.PosX;
                *p++ = f.RightSaber.PosY;
                *p++ = f.RightSaber.PosZ;
                *p++ = f.RightSaber.RotX;
                *p++ = f.RightSaber.RotY;
                *p++ = f.RightSaber.RotZ;
                *p++ = f.RightSaber.RotW;
                *p++ = f.Head.PosX;
                *p++ = f.Head.PosY;
                *p++ = f.Head.PosZ;
                *p++ = f.Head.RotX;
                *p++ = f.Head.RotY;
                *p++ = f.Head.RotZ;
                *p++ = f.Head.RotW;
                offset += MotionFrame.Size;
            }
        }
        return buffer;
    }
}
