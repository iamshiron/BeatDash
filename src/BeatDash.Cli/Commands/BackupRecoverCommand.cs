using Spectre.Console;
using Spectre.Console.Cli;

namespace Shiron.BeatDash.Cli.Commands;

/// <summary>Recovers the pre-restore snapshot after an interrupted or failed replacement.</summary>
public sealed class BackupRecoverCommand : AsyncCommand<BackupSettings> {
    /// <inheritdoc/>
    public override Task<int> ExecuteAsync(CommandContext context, BackupSettings settings, CancellationToken ct) => BackupClient.RunAsync(async () => {
        using var http = await BackupClient.LoginAsync(settings, ct);
        using var response = await http.PostAsync("/api/admin/backups/recover", null, ct);
        await BackupClient.EnsureSuccessAsync(response, ct);
        AnsiConsole.MarkupLine("[green]Recovered the pre-restore state. Maintenance mode remains enabled.[/]");
    });
}
