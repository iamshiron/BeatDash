namespace Shiron.BeatDash.Analysis.Extractors;

/// <summary>
/// Timing and readability features. Reaction time and jump distance follow Beat
/// Saber's half-jump-duration model and only appear when the map declares an NJS.
/// </summary>
public sealed class TimingFeatureExtractor : IFeatureExtractor {
    public const string SongLength = "timing.song_length_s";
    public const string Njs = "timing.njs";
    public const string ReactionTime = "timing.reaction_time_s";
    public const string JumpDistance = "timing.jump_distance";

    public string Name => "timing";

    public IReadOnlyList<FeatureDefinition> Provides { get; } = [
        new(SongLength, "Playable length in seconds"),
        new(Njs, "Declared note jump movement speed"),
        new(ReactionTime, "Seconds a note is readable before it must be hit (lower = harder)"),
        new(JumpDistance, "Distance a note travels while airborne"),
    ];

    public void Extract(FeatureContext context, FeatureBuilder features) {
        features.Set(SongLength, context.SongLengthSeconds);

        var njs = context.Beatmap.Njs;
        if (njs is not > 0) return;

        var njsOffset = context.Beatmap.NjsOffset ?? 0;
        var beatDuration = 60.0 / context.Bpm;

        var hjd = 4.0;
        while (njs.Value * beatDuration * hjd > 17.999) hjd /= 2;
        hjd = Math.Max(hjd + njsOffset, 0.25);

        var jumpDuration = beatDuration * hjd * 2;

        features.Set(Njs, njs.Value);
        features.Set(ReactionTime, jumpDuration / 2);
        features.Set(JumpDistance, njs.Value * jumpDuration);
    }
}
