using System.ComponentModel;
using System.Net.Http.Json;
using Shiron.BeatDash.Data.Backup;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shiron.BeatDash.Cli.Commands;

/// <summary>Downloads a complete server-generated backup archive.</summary>
public sealed class BackupExportCommand : AsyncCommand<BackupExportCommand.Settings> {
    /// <summary>Export destination and connection options.</summary>
    public sealed class Settings : BackupSettings {
        /// <summary>New local archive file.</summary>
        [CommandArgument(0, "<archive>")]
        [Description("Destination archive (must not already exist)")]
        public string Archive { get; init; } = "";
    }

    /// <inheritdoc/>
    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken ct) => BackupClient.RunAsync(async () => {
        var destination = Path.GetFullPath(settings.Archive);
        if (File.Exists(destination)) throw new IOException("Destination already exists.");
        var partial = destination + $".{Guid.NewGuid():N}.partial";
        try {
            using var http = await BackupClient.LoginAsync(settings, ct);
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/backups/export") {
                Content = JsonContent.Create(new BackupExportRequest(settings.Scope))
            };
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            await BackupClient.EnsureSuccessAsync(response, ct);
            var fileOptions = new FileStreamOptions {
                Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None, Options = FileOptions.Asynchronous
            };
            if (!OperatingSystem.IsWindows()) fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await using (var output = new FileStream(partial, fileOptions)) {
                await response.Content.CopyToAsync(output, ct);
                await output.FlushAsync(ct);
                output.Flush(flushToDisk: true);
            }
            File.Move(partial, destination);
            AnsiConsole.MarkupLineInterpolated($"[green]Backup saved:[/] {destination}");
        } finally { File.Delete(partial); }
    });
}
