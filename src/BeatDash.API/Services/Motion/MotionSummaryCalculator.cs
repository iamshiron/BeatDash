using System.Text.Json;
using Shiron.BeatDash.DB.Schema;

namespace Shiron.BeatDash.API.Services.Motion;

/// <summary>One bucketed fatigue sample: average saber speed (m/s) at a song time.</summary>
public sealed record MotionFatigueSample(int TMs, double LeftSpeed, double RightSpeed);

/// <summary>
/// Derives scalar movement metrics from a play's flattened motion samples
/// (<c>FrameCount * 21</c> floats: left saber, right saber, head — each
/// <c>PosXYZ, RotXYZW</c>). Pure and allocation-light so it can run inline on the
/// motion-persistence path and be unit-tested without a database.
/// </summary>
public static class MotionSummaryCalculator {
    private const int FloatsPerFrame = 21;
    private const int LeftPos = 0;   // left saber PosX offset within a frame
    private const int RightPos = 7;  // right saber PosX offset
    private const int HeadPos = 14;  // head PosX offset
    private const int MaxFatigueBuckets = 12;

    /// <summary>
    /// Computes the motion summary. Speeds are normalized by the song span
    /// (<paramref name="lastSongTimeMs"/>) rather than frame count, so decimation
    /// gaps don't inflate them. Returns zeroed metrics for degenerate inputs.
    /// </summary>
    public static PlaySessionMotionSummary Compute(
        float[] samples, int frameCount, int sampleRateHz, int lastSongTimeMs, Guid playSessionId) {
        // Clamp to what the buffer actually holds.
        frameCount = Math.Min(frameCount, samples.Length / FloatsPerFrame);

        var songSeconds = lastSongTimeMs > 0
            ? lastSongTimeMs / 1000.0
            : Math.Max(1, frameCount) / (double) Math.Max(1, sampleRateHz);

        double leftTravel = 0, rightTravel = 0, headTravel = 0;
        var leftBox = new BoundingBox();
        var rightBox = new BoundingBox();
        var headBox = new BoundingBox();

        var numBuckets = Math.Clamp(Math.Min(MaxFatigueBuckets, frameCount), 1, MaxFatigueBuckets);
        var bucketLeft = new double[numBuckets];
        var bucketRight = new double[numBuckets];

        for (var f = 0; f < frameCount; f++) {
            var o = f * FloatsPerFrame;
            leftBox.Add(samples[o + LeftPos], samples[o + LeftPos + 1], samples[o + LeftPos + 2]);
            rightBox.Add(samples[o + RightPos], samples[o + RightPos + 1], samples[o + RightPos + 2]);
            headBox.Add(samples[o + HeadPos], samples[o + HeadPos + 1], samples[o + HeadPos + 2]);

            if (f == 0) continue;
            var p = (f - 1) * FloatsPerFrame;
            var dl = Distance(samples, p + LeftPos, o + LeftPos);
            var dr = Distance(samples, p + RightPos, o + RightPos);
            var dh = Distance(samples, p + HeadPos, o + HeadPos);
            leftTravel += dl;
            rightTravel += dr;
            headTravel += dh;

            var bucket = Math.Min(numBuckets - 1, f * numBuckets / Math.Max(1, frameCount));
            bucketLeft[bucket] += dl;
            bucketRight[bucket] += dr;
        }

        var bucketSeconds = songSeconds / numBuckets;
        var fatigue = new List<MotionFatigueSample>(numBuckets);
        if (frameCount >= 2) {
            for (var b = 0; b < numBuckets; b++) {
                var tMs = (int) Math.Round((b + 0.5) / numBuckets * songSeconds * 1000);
                fatigue.Add(new MotionFatigueSample(
                    tMs,
                    bucketSeconds > 0 ? bucketLeft[b] / bucketSeconds : 0,
                    bucketSeconds > 0 ? bucketRight[b] / bucketSeconds : 0));
            }
        }

        return new PlaySessionMotionSummary {
            PlaySessionId = playSessionId,
            FrameCount = frameCount,
            SampleRateHz = sampleRateHz,
            LeftSaberTravel = leftTravel,
            RightSaberTravel = rightTravel,
            HeadTravel = headTravel,
            AvgLeftSaberSpeed = leftTravel / songSeconds,
            AvgRightSaberSpeed = rightTravel / songSeconds,
            LeftReachRange = leftBox.Diagonal,
            RightReachRange = rightBox.Diagonal,
            HeadRange = headBox.Diagonal,
            FatigueCurve = JsonSerializer.Serialize(fatigue),
        };
    }

    private static double Distance(float[] s, int a, int b) {
        double dx = s[a] - s[b], dy = s[a + 1] - s[b + 1], dz = s[a + 2] - s[b + 2];
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private struct BoundingBox() {
        private float _minX = float.MaxValue, _minY = float.MaxValue, _minZ = float.MaxValue;
        private float _maxX = float.MinValue, _maxY = float.MinValue, _maxZ = float.MinValue;
        private bool _any = false;

        public void Add(float x, float y, float z) {
            _any = true;
            if (x < _minX) _minX = x;
            if (y < _minY) _minY = y;
            if (z < _minZ) _minZ = z;
            if (x > _maxX) _maxX = x;
            if (y > _maxY) _maxY = y;
            if (z > _maxZ) _maxZ = z;
        }

        public readonly double Diagonal {
            get {
                if (!_any) return 0;
                double dx = _maxX - _minX, dy = _maxY - _minY, dz = _maxZ - _minZ;
                return Math.Sqrt(dx * dx + dy * dy + dz * dz);
            }
        }
    }
}
