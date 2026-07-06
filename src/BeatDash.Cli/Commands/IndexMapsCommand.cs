using System.ComponentModel;
using System.Text.Json;
using Shiron.BeatDash.Beatmaps;
using Spectre.Console.Cli;

namespace Shiron.BeatDash.Cli.Commands;

/// <summary>
/// Parses every custom-level folder under a root directory and writes one compact
/// JSON summary per map to stdout (JSON Lines). Failures are emitted as
/// <c>{"folder":..,"error":..}</c> lines rather than aborting the run — useful for
/// sweeping a whole collection while debugging the parser.
/// </summary>
public sealed class IndexMapsCommand : Command<IndexMapsCommand.Settings> {
    public sealed class Settings : CommandSettings {
        [CommandArgument(0, "<root>")]
        [Description("Directory containing many custom-level folders")]
        public string Root { get; init; } = "";
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation) {
        if (!Directory.Exists(settings.Root)) {
            Console.Error.WriteLine($"Directory not found: {settings.Root}");
            return 1;
        }

        var writer = Console.Out;
        foreach (var dir in Directory.EnumerateDirectories(settings.Root).OrderBy(x => x, StringComparer.Ordinal)) {
            try {
                var level = BeatmapParser.ParseLevel(new DirectoryBeatmapFileSource(dir));
                writer.WriteLine(BeatmapJson.SerializeSummary(level));
            } catch (Exception ex) {
                var error = JsonSerializer.Serialize(new { folder = Path.GetFileName(dir), error = ex.Message });
                writer.WriteLine(error);
            }
        }

        return 0;
    }
}
