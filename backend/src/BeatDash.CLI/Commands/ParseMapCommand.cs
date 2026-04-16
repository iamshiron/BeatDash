using System.Text.Json;
using Shiron.BeatDash.Data.Maps;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Json;

namespace Shiron.BeatDash.CLI.Commands;

public class ParseMapCommand : AsyncCommand<ParseMapCommand.Settings> {
    public sealed class Settings : CommandSettings {
        [CommandArgument(0, "<folder>")] public required string Folder { get; set; }
    }

    protected async override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken) {
        if (!Directory.Exists(settings.Folder))
            throw new ArgumentException("Directory could not be found", nameof(settings.Folder));

        try {
            var map = await BeatSaberMap.LoadAsync(settings.Folder);
            await File.OpenWrite("map.json").WriteAsync(JsonSerializer.SerializeToUtf8Bytes(map));
            AnsiConsole.MarkupLine($"[green]Map saved to [white]map.json[/][/]");
        } catch (Exception e) {
            AnsiConsole.WriteException(e);
            return -1;
        }

        return 0;
    }
}
