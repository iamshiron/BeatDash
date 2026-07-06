namespace Shiron.BeatDash.Analysis.Extractors;

/// <summary>Object counts and note densities (notes per second).</summary>
public sealed class CountFeatureExtractor : IFeatureExtractor {
    public const string Notes = "count.notes";
    public const string Bombs = "count.bombs";
    public const string Obstacles = "count.obstacles";
    public const string Chains = "count.chains";
    public const string Arcs = "count.arcs";
    public const string Nps = "density.nps";
    public const string NpsLeft = "density.nps_left";
    public const string NpsRight = "density.nps_right";
    public const string BombNps = "density.bomb_nps";
    public const string SliderNps = "density.slider_nps";
    public const string DotRatio = "pattern.dot_ratio";

    public string Name => "counts";

    public IReadOnlyList<FeatureDefinition> Provides { get; } = [
        new(Notes, "Total note count"),
        new(Bombs, "Total bomb count"),
        new(Obstacles, "Total obstacle (wall) count"),
        new(Chains, "Total chain (burst slider) count"),
        new(Arcs, "Total arc (slider) count"),
        new(Nps, "Notes per second over the playable length"),
        new(NpsLeft, "Left-hand notes per second"),
        new(NpsRight, "Right-hand notes per second"),
        new(BombNps, "Bombs per second"),
        new(SliderNps, "Arcs + chains per second"),
        new(DotRatio, "Fraction of notes that are dots (any-direction)"),
    ];

    public void Extract(FeatureContext context, FeatureBuilder features) {
        var bm = context.Beatmap;
        var length = context.SongLengthSeconds;

        features.Set(Notes, bm.Notes.Count);
        features.Set(Bombs, bm.Bombs.Count);
        features.Set(Obstacles, bm.Obstacles.Count);
        features.Set(Chains, bm.Chains.Count);
        features.Set(Arcs, bm.Arcs.Count);

        var left = 0;
        var right = 0;
        var dots = 0;
        foreach (var n in bm.Notes) {
            if (n.Color == 0) left++;
            else if (n.Color == 1) right++;
            if (n.Direction == 8) dots++;
        }

        features.Set(Nps, bm.Notes.Count / length);
        features.Set(NpsLeft, left / length);
        features.Set(NpsRight, right / length);
        features.Set(BombNps, bm.Bombs.Count / length);
        features.Set(SliderNps, (bm.Chains.Count + bm.Arcs.Count) / length);
        features.Set(DotRatio, bm.Notes.Count > 0 ? (double) dots / bm.Notes.Count : 0);
    }
}
