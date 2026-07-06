using Shiron.BeatDash.Beatmaps;

namespace Shiron.BeatDash.Analysis;

/// <summary>A single swing: one or more same-hand notes struck at (almost) the same time.</summary>
/// <param name="TimeSeconds">When the swing occurs.</param>
/// <param name="X">Average column of the grouped notes.</param>
/// <param name="Y">Average row of the grouped notes.</param>
/// <param name="Direction">Cut direction of the leading note (0..8, 8 = dot).</param>
public readonly record struct Swing(double TimeSeconds, double X, double Y, int Direction);

/// <summary>
/// Shared, precomputed inputs for all feature extractors — built once per difficulty
/// so each extractor is cheap and independent. Holds the parsed map, its timing, and
/// the derived per-hand swing series.
/// </summary>
public sealed class FeatureContext {
    // Notes closer than this (in beats) collapse into one swing (stacks / exact windows).
    private const double SwingGroupBeatEpsilon = 1e-3;

    public ParsedBeatmap Beatmap { get; }
    public double Bpm { get; }
    public BeatTimeConverter Time { get; }

    /// <summary>Total playable length in seconds (last object end), always &gt; 0.</summary>
    public double SongLengthSeconds { get; }

    /// <summary>Ascending note times in seconds.</summary>
    public IReadOnlyList<double> NoteTimesSeconds { get; }

    /// <summary>Left-hand (red, color 0) swing series, ascending in time.</summary>
    public IReadOnlyList<Swing> LeftSwings { get; }

    /// <summary>Right-hand (blue, color 1) swing series, ascending in time.</summary>
    public IReadOnlyList<Swing> RightSwings { get; }

    private FeatureContext(
        ParsedBeatmap beatmap, double bpm, BeatTimeConverter time, double songLengthSeconds,
        IReadOnlyList<double> noteTimesSeconds, IReadOnlyList<Swing> left, IReadOnlyList<Swing> right) {
        Beatmap = beatmap;
        Bpm = bpm;
        Time = time;
        SongLengthSeconds = songLengthSeconds;
        NoteTimesSeconds = noteTimesSeconds;
        LeftSwings = left;
        RightSwings = right;
    }

    /// <summary>Builds the shared context. Assumes <paramref name="bpm"/> &gt; 0.</summary>
    public static FeatureContext Build(ParsedBeatmap beatmap, double bpm) {
        var time = BeatTimeConverter.Build(bpm, beatmap.BpmChanges);

        var lastBeat = 0.0;
        foreach (var n in beatmap.Notes) lastBeat = Math.Max(lastBeat, n.Beat);
        foreach (var b in beatmap.Bombs) lastBeat = Math.Max(lastBeat, b.Beat);
        foreach (var o in beatmap.Obstacles) lastBeat = Math.Max(lastBeat, o.Beat + o.Duration);
        foreach (var c in beatmap.Chains) lastBeat = Math.Max(lastBeat, c.TailBeat);
        foreach (var a in beatmap.Arcs) lastBeat = Math.Max(lastBeat, a.TailBeat);

        var songLength = Math.Max(time.ToSeconds(lastBeat), 1e-3);

        var noteTimes = beatmap.Notes
            .Select(n => time.ToSeconds(n.Beat))
            .OrderBy(t => t)
            .ToList();

        var left = BuildSwings(beatmap, time, color: 0);
        var right = BuildSwings(beatmap, time, color: 1);

        return new FeatureContext(beatmap, bpm, time, songLength, noteTimes, left, right);
    }

    private static List<Swing> BuildSwings(ParsedBeatmap beatmap, BeatTimeConverter time, int color) {
        var notes = beatmap.Notes.Where(n => n.Color == color).OrderBy(n => n.Beat).ToList();
        var decode = beatmap.UsesMappingExtensions;
        var swings = new List<Swing>();

        var i = 0;
        while (i < notes.Count) {
            var groupBeat = notes[i].Beat;
            double sumX = 0, sumY = 0;
            var count = 0;
            var direction = notes[i].Direction;

            while (i < notes.Count && notes[i].Beat - groupBeat <= SwingGroupBeatEpsilon) {
                sumX += decode ? MappingExtensions.Decode(notes[i].X) : notes[i].X;
                sumY += decode ? MappingExtensions.Decode(notes[i].Y) : notes[i].Y;
                count++;
                i++;
            }

            swings.Add(new Swing(time.ToSeconds(groupBeat), sumX / count, sumY / count, direction));
        }

        return swings;
    }
}
