using System.IO.Compression;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Shiron.BeatDash.Data.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shiron.BeatDash.CLI.Commands;

public class UploadCommand : AsyncCommand<UploadCommand.Settings> {
    public sealed class Settings : CommandSettings {
        [CommandArgument(0, "<file>")] public required string File { get; set; }

        [CommandOption("-h|--host")] public string Host { get; set; } = "http://127.0.0.1:5000";
    }

    protected async override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken) {
        if (!File.Exists(settings.File)) {
            AnsiConsole.MarkupLine($"[red]File not found:[/] {settings.File}");
            return 1;
        }

        var fileInfo = new FileInfo(settings.File);
        AnsiConsole.MarkupLine($"[grey]Uploading [green]{settings.File}[/] ({fileInfo.Length / 1024 / 1024}MB)...[/]");

        var url = $"{settings.Host.TrimEnd('/')}/recordings/upload";

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        using var fileStream = File.OpenRead(settings.File);
        using var compressed = new MemoryStream();
        await using (var gzip = new GZipStream(compressed, CompressionLevel.Fastest, true)) {
            await fileStream.CopyToAsync(gzip, cancellationToken);
        }
        compressed.Position = 0;

        AnsiConsole.MarkupLine($"[grey]Compressed to [green]{compressed.Length / 1024 / 1024}MB[/][/]");

        using var content = new StreamContent(compressed);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        content.Headers.ContentEncoding.Add("gzip");

        var response = await http.PostAsync(url, content, cancellationToken);

        if (!response.IsSuccessStatusCode) {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            AnsiConsole.MarkupLine($"[red]Upload failed[/] ({(int) response.StatusCode}): {error.EscapeMarkup()}");
            return 1;
        }

        var result = await response.Content.ReadFromJsonAsync<UploadResult>(cancellationToken);
        if (result != null) {
            AnsiConsole.MarkupLine($"[green]Upload complete[/] — [bold]{result.Sessions}[/] sessions, [bold]{result.Snapshots}[/] snapshots");
        }

        return 0;
    }

    private record UploadResult(int Sessions, int Snapshots);
}
