using System.ComponentModel;
using Shiron.BeatDash.Analysis;
using Shiron.BeatDash.Beatmaps;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shiron.BeatDash.Cli.Commands;

/// <summary>Parses one custom-level folder and prints its unified model.</summary>
public sealed class ParseMapCommand : Command<ParseMapCommand.Settings> {
    public sealed class Settings : CommandSettings {
        [CommandArgument(0, "<directory>")]
        [Description("Path to the extracted custom level folder (must contain Info.dat)")]
        public string Directory { get; init; } = "";

        [CommandOption("--json")]
        [Description("Emit the raw unified model as JSON (pipe-safe) instead of a table")]
        public bool Json { get; init; }

        [CommandOption("--notes")]
        [Description("Also list the first notes of each difficulty in the table view")]
        public bool Notes { get; init; }

        [CommandOption("--features")]
        [Description("Run the feature extractor and print the feature set for each difficulty")]
        public bool Features { get; init; }

        [CommandOption("--metrics")]
        [Description("Run the metric scorer and print difficulty / PP / characteristics per difficulty")]
        public bool Metrics { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation) {
        if (!System.IO.Directory.Exists(settings.Directory)) {
            AnsiConsole.MarkupLineInterpolated($"[red]Directory not found:[/] {settings.Directory}");
            return 1;
        }

        ParsedLevel level;
        try {
            level = BeatmapParser.ParseLevel(new DirectoryBeatmapFileSource(settings.Directory));
        } catch (UnknownFormatException ex) {
            AnsiConsole.MarkupLineInterpolated($"[red]Parse failed:[/] {ex.Message}");
            return 1;
        }

        if (settings.Json) {
            // Plain stdout, no markup, so the output stays pipe-safe.
            Console.WriteLine(BeatmapJson.SerializeIndented(level));
            return 0;
        }

        RenderLevel(level, settings.Notes);
        if (settings.Features) RenderFeatures(level);
        if (settings.Metrics) RenderMetrics(level);
        return 0;
    }

    private static void RenderMetrics(ParsedLevel level) {
        var extractor = FeatureExtractor.CreateDefault();
        var scorer = MetricScorer.CreateDefault(MetricConfig.CreateDefault());

        foreach (var b in level.Beatmaps) {
            var features = extractor.Extract(b, level.Bpm);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(b.Characteristic)} / {Markup.Escape(b.Difficulty)}[/] — metrics");

            if (!features.IsSuccess) {
                AnsiConsole.MarkupLine($"  [red]features unavailable: {features.Outcome}[/]");
                continue;
            }

            var result = scorer.Score(features.Features);
            if (!result.IsSuccess) {
                var failed = result.FailedScorer is { } f ? $" (scorer: {Markup.Escape(f)})" : "";
                AnsiConsole.MarkupLine($"  [red]scoring failed: {result.Outcome}{failed}[/]");
                continue;
            }

            var table = new Table().Border(TableBorder.Minimal);
            table.AddColumns("Metric", "Value");
            foreach (var (key, value) in result.Metrics.OrderBy(kv => kv.Key, StringComparer.Ordinal)) {
                table.AddRow(Markup.Escape(key), value.ToString("0.####"));
            }
            AnsiConsole.Write(table);
        }
    }

    private static void RenderFeatures(ParsedLevel level) {
        var extractor = FeatureExtractor.CreateDefault();
        foreach (var b in level.Beatmaps) {
            var result = extractor.Extract(b, level.Bpm);
            AnsiConsole.WriteLine();
            var failed = result.FailedExtractor is { } f ? $" [red](extractor: {Markup.Escape(f)})[/]" : "";
            AnsiConsole.MarkupLine(
                $"[grey]{Markup.Escape(b.Characteristic)} / {Markup.Escape(b.Difficulty)}[/] — " +
                $"features: [yellow]{result.Outcome}[/]{failed}");

            if (result.Features.Count == 0) continue;

            var table = new Table().Border(TableBorder.Minimal);
            table.AddColumns("Feature", "Value");
            foreach (var (key, value) in result.Features.OrderBy(kv => kv.Key, StringComparer.Ordinal)) {
                table.AddRow(Markup.Escape(key), value.ToString("0.####"));
            }
            AnsiConsole.Write(table);
        }
    }

    private static void RenderLevel(ParsedLevel level, bool showNotes) {
        AnsiConsole.Write(new Rule($"[yellow]{Markup.Escape(level.SongName)}[/]").LeftJustified());

        var meta = new Grid().AddColumn().AddColumn();
        meta.AddRow("[grey]Song[/]", Markup.Escape(FormatSongTitle(level)));
        meta.AddRow("[grey]Author[/]", Markup.Escape(level.SongAuthor));
        meta.AddRow("[grey]Mapper[/]", Markup.Escape(level.Mapper));
        meta.AddRow("[grey]BPM[/]", level.Bpm.ToString("0.###"));
        meta.AddRow("[grey]Info version[/]", Markup.Escape(level.InfoVersion));
        meta.AddRow("[grey]Folder[/]", Markup.Escape(level.Folder));
        AnsiConsole.Write(meta);
        AnsiConsole.WriteLine();

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumns("Char", "Difficulty", "Fmt", "NJS", "Offset",
            "Notes", "Bombs", "Walls", "Chains", "Arcs", "BPMΔ");
        foreach (var b in level.Beatmaps) {
            table.AddRow(
                Markup.Escape(b.Characteristic),
                Markup.Escape(b.Difficulty),
                Markup.Escape(b.FormatVersion),
                b.Njs?.ToString("0.###") ?? "-",
                b.NjsOffset?.ToString("0.###") ?? "-",
                b.Notes.Count.ToString(),
                b.Bombs.Count.ToString(),
                b.Obstacles.Count.ToString(),
                b.Chains.Count.ToString(),
                b.Arcs.Count.ToString(),
                b.BpmChanges.Count.ToString());
        }
        AnsiConsole.Write(table);

        if (showNotes) {
            foreach (var b in level.Beatmaps) {
                RenderNotes(b);
            }
        }
    }

    private static void RenderNotes(ParsedBeatmap b) {
        const int limit = 25;
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLineInterpolated(
            $"[grey]{b.Characteristic} / {b.Difficulty}[/] — first {Math.Min(limit, b.Notes.Count)} of {b.Notes.Count} notes");

        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumns("Beat", "X", "Y", "Color", "Dir", "AngleOff");
        foreach (var n in b.Notes.Take(limit)) {
            table.AddRow(
                n.Beat.ToString("0.###"),
                n.X.ToString(),
                n.Y.ToString(),
                n.Color == 0 ? "[red]red[/]" : "[blue]blue[/]",
                n.Direction.ToString(),
                n.AngleOffset.ToString("0.###"));
        }
        AnsiConsole.Write(table);
    }

    private static string FormatSongTitle(ParsedLevel level) =>
        string.IsNullOrEmpty(level.SongSubName) ? level.SongName : $"{level.SongName} {level.SongSubName}";
}
