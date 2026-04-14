using Spectre.Console;
using Spectre.Console.Cli;

namespace Shiron.BeatDash.CLI.Commands;

public sealed class ReplayCommand : AsyncCommand<ReplayCommand.Settings> {
    public sealed class Settings : CommandSettings {
        [CommandArgument(0, "<file>")] public required string File { get; set; }
    }

    protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken) {
        if (!File.Exists(settings.File)) throw new FileNotFoundException();

        AnsiConsole.MarkupLine($"[green]Replaying [white]{settings.File}[/][/]");

        return Task.FromResult(0);
    }
}
