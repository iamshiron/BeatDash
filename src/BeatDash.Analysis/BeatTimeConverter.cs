using Shiron.BeatDash.Beatmaps;

namespace Shiron.BeatDash.Analysis;

/// <summary>
/// Converts beat positions to seconds, honoring mid-song BPM changes as a set of
/// piecewise-constant tempo segments. With no changes this is simply
/// <c>beat / bpm * 60</c>.
/// </summary>
public sealed class BeatTimeConverter {
    private readonly double[] _beats;         // ascending segment start beats; _beats[0] == 0
    private readonly double[] _bpms;          // tempo within each segment
    private readonly double[] _startSeconds;  // cumulative seconds at each segment start

    private BeatTimeConverter(double[] beats, double[] bpms, double[] startSeconds) {
        _beats = beats;
        _bpms = bpms;
        _startSeconds = startSeconds;
    }

    /// <summary>
    /// Builds a converter from a base BPM and any in-map BPM changes.
    /// <paramref name="baseBpm"/> must be positive (validated by the caller).
    /// </summary>
    public static BeatTimeConverter Build(double baseBpm, IReadOnlyList<BpmChange> changes) {
        // Collapse to strictly-increasing (beat -> bpm) points starting at beat 0.
        var points = new SortedDictionary<double, double> { [0.0] = baseBpm };
        foreach (var change in changes) {
            if (change.Beat > 0 && change.Bpm > 0) {
                points[change.Beat] = change.Bpm; // later change at the same beat wins
            }
        }

        var beats = new double[points.Count];
        var bpms = new double[points.Count];
        var startSeconds = new double[points.Count];

        var i = 0;
        foreach (var (beat, bpm) in points) {
            beats[i] = beat;
            bpms[i] = bpm;
            if (i > 0) {
                startSeconds[i] = startSeconds[i - 1] + (beats[i] - beats[i - 1]) / bpms[i - 1] * 60.0;
            }
            i++;
        }

        return new BeatTimeConverter(beats, bpms, startSeconds);
    }

    /// <summary>Converts a beat position to elapsed seconds from the start of the song.</summary>
    public double ToSeconds(double beat) {
        // Find the last segment whose start beat is <= beat.
        var seg = 0;
        for (var i = 1; i < _beats.Length; i++) {
            if (_beats[i] <= beat) seg = i;
            else break;
        }
        return _startSeconds[seg] + (beat - _beats[seg]) / _bpms[seg] * 60.0;
    }
}
