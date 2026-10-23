using System.ComponentModel;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Shiron.BeatDash.Data.Backup;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shiron.BeatDash.Cli.Commands;

/// <summary>Restores an archive through the admin backend API.</summary>
public sealed class BackupRestoreCommand : AsyncCommand<BackupRestoreCommand.Settings> {
    /// <summary>Archive source and restore options.</summary>
    public sealed class Settings : BackupSettings {
        /// <summary>The archive previously produced by the backend.</summary>
        [CommandArgument(0, "<archive>")]
        public string Archive { get; init; } = "";

        /// <summary>Explicit acknowledgement that the selected server state will be replaced.</summary>
        [CommandOption("--yes")]
        [Description("Confirm replacement of the selected server state without prompting")]
        public bool Yes { get; init; }
    }

    /// <inheritdoc/>
    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken ct) => BackupClient.RunAsync(async () => {
        await using var input = File.OpenRead(settings.Archive);
        if (!settings.Yes && (Console.IsInputRedirected
            || !AnsiConsole.Confirm($"Replace {settings.Scope} on {Markup.Escape(settings.Host)} from this backup?", defaultValue: false)))
            throw new InvalidOperationException("Restore cancelled. Unattended restore requires --yes.");
        using var http = await BackupClient.LoginAsync(settings, ct);
        using var content = new StreamContent(input);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        using var response = await http.PostAsync($"/api/admin/backups/restore?scope={settings.Scope}", content, ct);
        await BackupClient.EnsureSuccessAsync(response, ct);
        var result = await response.Content.ReadFromJsonAsync<BackupRestoreResponse>(ct)
            ?? throw new InvalidDataException("Missing restore result.");
        AnsiConsole.MarkupLineInterpolated($"[green]Restored:[/] {result.Tables} tables, {result.Objects} objects ({result.Scope}).");
    });
}
