using System.ComponentModel;
using System.Text.Json;
using Shiron.BeatDash.Analysis;
using Shiron.BeatDash.Beatmaps;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shiron.BeatDash.Cli.Commands;

/// <summary>
/// Fits calibration <c>Scale</c> factors for the metric models against a corpus of
/// maps and prints the suggested "Metrics" config. Non-destructive: it applies
/// nothing — it only reports the values you would set (paste them into config, or
/// save with <c>--out</c>). The model weights are left untouched; only each model's
/// <c>Scale</c> (and the PP multiplier) are suggested.
/// </summary>
public sealed class CalibrateCommand : Command<CalibrateCommand.Settings> {
    public sealed class Settings : CommandSettings {
        [CommandArgument(0, "<root>")]
        [Description("Directory containing many custom-level folders")]
        public string Root { get; init; } = "";

        [CommandOption("--percentile")]
        [Description("Raw-value percentile that maps to 1.0 (default 0.98)")]
        public double Percentile { get; init; } = 0.98;

        [CommandOption("--pp-ceiling")]
        [Description("Target base PP for a top-difficulty map (sets Pp.Multiplier; default 700)")]
        public double PpCeiling { get; init; } = 700;

        [CommandOption("--out")]
        [Description("Also write the suggested config snippet to this JSON file")]
        public string? Out { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation) {
        if (!Directory.Exists(settings.Root)) {
            Console.Error.WriteLine($"Directory not found: {settings.Root}");
            return 1;
        }

        var config = MetricConfig.CreateDefault();
        var extractor = FeatureExtractor.CreateDefault();

        // Raw (un-normalized) value samples per model, gathered across the corpus.
        var difficultyRaw = new List<double>();
        var characteristicRaw = config.Characteristics.Keys.ToDictionary(k => k, _ => new List<double>());

        var maps = 0;
        var difficulties = 0;
        var skipped = 0;

        foreach (var dir in Directory.EnumerateDirectories(settings.Root)) {
            cancellation.ThrowIfCancellationRequested();

            ParsedLevel level;
            try {
                level = BeatmapParser.ParseLevel(new DirectoryBeatmapFileSource(dir));
            } catch {
                skipped++;
                continue;
            }

            maps++;
            foreach (var beatmap in level.Beatmaps) {
                var features = extractor.Extract(beatmap, level.Bpm);
                if (!features.IsSuccess) {
                    skipped++;
                    continue;
                }

                difficulties++;
                difficultyRaw.Add(config.Difficulty.Evaluate(features.Features));
                foreach (var (name, model) in config.Characteristics) {
                    characteristicRaw[name].Add(model.Evaluate(features.Features));
                }
            }

            if (maps % 500 == 0) Console.Error.WriteLine($"  …{maps} maps, {difficulties} difficulties");
        }

        if (difficulties == 0) {
            Console.Error.WriteLine("No difficulties could be analyzed; nothing to calibrate.");
            return 1;
        }

        var p = Math.Clamp(settings.Percentile, 0.5, 1.0);
        var difficultyScale = Percentile(difficultyRaw, p);
        var characteristicScales = characteristicRaw.ToDictionary(kv => kv.Key, kv => Percentile(kv.Value, p));

        RenderReport(settings, maps, difficulties, skipped, p, difficultyRaw, difficultyScale, characteristicRaw, characteristicScales);

        var snippet = BuildSnippet(settings, difficultyScale, characteristicScales);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Suggested config (nothing applied) — paste under your configuration root:[/]");
        Console.WriteLine(snippet);

        if (settings.Out is { Length: > 0 } outPath) {
            File.WriteAllText(outPath, snippet);
            AnsiConsole.MarkupLineInterpolated($"[green]Wrote snippet to[/] {outPath}");
        }

        return 0;
    }

    private static void RenderReport(
        Settings settings, int maps, int difficulties, int skipped, double p,
        List<double> difficultyRaw, double difficultyScale,
        Dictionary<string, List<double>> characteristicRaw, Dictionary<string, double> characteristicScales) {
        AnsiConsole.MarkupLineInterpolated(
            $"[grey]Analyzed[/] {difficulties} [grey]difficulties across[/] {maps} [grey]maps ({skipped} skipped), percentile[/] {p:0.###}");

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumns("Model", "median (raw)", $"p{p * 100:0} (raw)", "max (raw)", "→ Scale");

        AddRow(table, "difficulty", difficultyRaw, difficultyScale);
        foreach (var (name, values) in characteristicRaw) {
            AddRow(table, $"characteristic.{name}", values, characteristicScales[name]);
        }
        AnsiConsole.Write(table);

        AnsiConsole.MarkupLineInterpolated(
            $"[grey]Pp.Multiplier[/] → {settings.PpCeiling:0.###} [grey](base PP at top difficulty; weights & exponent unchanged)[/]");
    }

    private static void AddRow(Table table, string model, List<double> values, double scale) {
        var warn = scale <= 0 ? " [red](≤0 — review weights)[/]" : "";
        table.AddRow(
            Markup.Escape(model),
            Percentile(values, 0.5).ToString("0.###"),
            Percentile(values, 0.98).ToString("0.###"),
            FeatureMax(values).ToString("0.###"),
            $"{scale:0.###}{warn}");
    }

    private static string BuildSnippet(Settings settings, double difficultyScale, Dictionary<string, double> characteristicScales) {
        var snippet = new {
            Metrics = new {
                Difficulty = new { Scale = Round(difficultyScale) },
                Pp = new { Multiplier = settings.PpCeiling },
                Characteristics = characteristicScales.ToDictionary(
                    kv => kv.Key, kv => (object) new { Scale = Round(kv.Value) }),
            },
        };
        return JsonSerializer.Serialize(snippet, new JsonSerializerOptions { WriteIndented = true });
    }

    private static double Round(double v) => Math.Round(v, 4);

    private static double FeatureMax(IReadOnlyList<double> values) {
        var max = double.NegativeInfinity;
        foreach (var v in values) {
            if (v > max) max = v;
        }
        return values.Count == 0 ? 0 : max;
    }

    private static double Percentile(List<double> values, double p) {
        if (values.Count == 0) return 0;
        if (values.Count == 1) return values[0];

        var sorted = values.ToArray();
        Array.Sort(sorted);

        var rank = p * (sorted.Length - 1);
        var lo = (int) Math.Floor(rank);
        var hi = (int) Math.Ceiling(rank);
        if (lo == hi) return sorted[lo];
        var frac = rank - lo;
        return sorted[lo] * (1 - frac) + sorted[hi] * frac;
    }
}
