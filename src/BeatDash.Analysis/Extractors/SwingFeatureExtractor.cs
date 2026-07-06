using Shiron.BeatDash.Beatmaps;

namespace Shiron.BeatDash.Analysis.Extractors;

/// <summary>
/// Per-hand swing dynamics: how fast the sabers must travel, how sharply swing
/// angles change, how often parity resets, and how often hands cross the midline.
/// </summary>
public sealed class SwingFeatureExtractor : IFeatureExtractor {
    public const string HandSpeedMean = "swing.hand_speed_mean";
    public const string HandSpeedMax = "swing.hand_speed_max";
    public const string HandSpeedP95 = "swing.hand_speed_p95";
    public const string AngleChangeMean = "swing.angle_change_mean";
    public const string ResetRate = "swing.reset_rate";
    public const string CrossoverRate = "swing.crossover_rate";

    // Cut-direction (0..7) travel angle in degrees; index 8 (dot) has no angle.
    private static readonly double[] DirectionAngles = [90, 270, 180, 0, 135, 45, 225, 315];

    public string Name => "swing";

    public IReadOnlyList<FeatureDefinition> Provides { get; } = [
        new(HandSpeedMean, "Mean saber travel speed between swings (grid units/s)"),
        new(HandSpeedMax, "Peak saber travel speed between swings (grid units/s)"),
        new(HandSpeedP95, "95th-percentile saber travel speed (grid units/s)"),
        new(AngleChangeMean, "Mean change in swing angle between consecutive swings (degrees)"),
        new(ResetRate, "Fraction of swing transitions that break up/down parity (resets)"),
        new(CrossoverRate, "Fraction of notes on the opposite hand's side of the grid"),
    ];

    public void Extract(FeatureContext context, FeatureBuilder features) {
        var speeds = new List<double>();
        var angleChanges = new List<double>();
        var transitions = 0;
        var resets = 0;

        foreach (var swings in new[] { context.LeftSwings, context.RightSwings }) {
            for (var i = 1; i < swings.Count; i++) {
                var prev = swings[i - 1];
                var cur = swings[i];
                transitions++;

                var dt = cur.TimeSeconds - prev.TimeSeconds;
                if (dt > 1e-4) {
                    var dist = Math.Sqrt(Sq(cur.X - prev.X) + Sq(cur.Y - prev.Y));
                    speeds.Add(dist / dt);
                }

                var angleChange = AngleChange(prev.Direction, cur.Direction);
                if (angleChange.HasValue) angleChanges.Add(angleChange.Value);

                var prevVert = VerticalClass(prev.Direction);
                if (prevVert != 0 && prevVert == VerticalClass(cur.Direction)) resets++;
            }
        }

        if (speeds.Count > 0) {
            features.Set(HandSpeedMean, FeatureMath.Mean(speeds));
            features.Set(HandSpeedMax, FeatureMath.Max(speeds));
            features.Set(HandSpeedP95, FeatureMath.Percentile(speeds, 0.95));
        }
        if (angleChanges.Count > 0) {
            features.Set(AngleChangeMean, FeatureMath.Mean(angleChanges));
        }
        features.Set(ResetRate, transitions > 0 ? (double) resets / transitions : 0);
        features.Set(CrossoverRate, CrossoverRate_(context));
    }

    private static double CrossoverRate_(FeatureContext context) {
        var notes = context.Beatmap.Notes;
        if (notes.Count == 0) return 0;

        var decode = context.Beatmap.UsesMappingExtensions;
        var crossed = 0;
        foreach (var n in notes) {
            var x = decode ? MappingExtensions.Decode(n.X) : n.X;
            // Left hand (red) belongs on the left half; right hand (blue) on the right.
            if ((n.Color == 0 && x >= 2) || (n.Color == 1 && x <= 1)) crossed++;
        }
        return (double) crossed / notes.Count;
    }

    private static double? AngleChange(int dirA, int dirB) {
        if (dirA is < 0 or > 7 || dirB is < 0 or > 7) return null; // skip dots/unknown
        var diff = Math.Abs(DirectionAngles[dirA] - DirectionAngles[dirB]) % 360;
        return diff > 180 ? 360 - diff : diff;
    }

    private static int VerticalClass(int direction) => direction switch {
        0 or 4 or 5 => 1,   // up-ish
        1 or 6 or 7 => -1,  // down-ish
        _ => 0,             // horizontal or dot
    };

    private static double Sq(double v) => v * v;
}
