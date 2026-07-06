using Shiron.BeatDash.Analysis.Extractors;
using Shiron.BeatDash.Beatmaps;

namespace Shiron.BeatDash.Analysis;

/// <summary>
/// Runs a set of <see cref="IFeatureExtractor"/> plugins over a parsed difficulty,
/// producing a flat, named feature set. Validates preconditions up front and isolates
/// each extractor so one failure yields a diagnosable result rather than a crash.
/// </summary>
public sealed class FeatureExtractor {
    private static readonly IReadOnlyDictionary<string, double> Empty =
        new Dictionary<string, double>();

    private readonly IReadOnlyList<IFeatureExtractor> _extractors;

    public FeatureExtractor(IEnumerable<IFeatureExtractor> extractors) {
        _extractors = extractors.ToList();
    }

    /// <summary>
    /// The default pipeline. This list is the single place features are registered —
    /// add an extractor here to extend the feature set everywhere it's used.
    /// </summary>
    public static FeatureExtractor CreateDefault() => new([
        new CountFeatureExtractor(),
        new TimingFeatureExtractor(),
        new SwingFeatureExtractor(),
        new DensityFeatureExtractor(),
    ]);

    /// <summary>Every feature the configured extractors can emit.</summary>
    public IReadOnlyList<FeatureDefinition> Catalog =>
        _extractors.SelectMany(e => e.Provides).ToList();

    /// <summary>Extracts features for one difficulty using the given base <paramref name="bpm"/>.</summary>
    public FeatureExtractionResult Extract(ParsedBeatmap beatmap, double bpm) {
        if (bpm <= 0) return new FeatureExtractionResult(FeatureExtractionOutcome.InvalidTiming, Empty, null);
        if (beatmap.Notes.Count == 0) return new FeatureExtractionResult(FeatureExtractionOutcome.NoNotes, Empty, null);

        FeatureContext context;
        try {
            context = FeatureContext.Build(beatmap, bpm);
        } catch {
            return new FeatureExtractionResult(FeatureExtractionOutcome.InvalidTiming, Empty, null);
        }

        var builder = new FeatureBuilder();
        foreach (var extractor in _extractors) {
            try {
                extractor.Extract(context, builder);
            } catch {
                return new FeatureExtractionResult(FeatureExtractionOutcome.Failed, builder.Build(), extractor.Name);
            }
        }

        return new FeatureExtractionResult(FeatureExtractionOutcome.Success, builder.Build(), null);
    }
}
