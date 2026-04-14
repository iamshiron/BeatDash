using System.Collections.Concurrent;
using System.Text.Json;
using Shiron.BeatDash.Data;
using Shiron.BeatDash.Data.Models;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;

namespace Shiron.BeatDash.CLI.Commands;

public sealed class RecordCommand : AsyncCommand<RecordCommand.Settings> {
    public sealed class Settings : CommandSettings {
        [CommandArgument(0, "[output]")] public string Output { get; set; } = "./.recordings";

        [CommandOption("-h|--host")] public string Host { get; set; } = "ws://127.0.0.1:2946";
    }

    protected async override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken) {
        if (!Directory.Exists(settings.Output))
            Directory.CreateDirectory(settings.Output);

        var outFile = Path.Join(settings.Output, $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json");

        var messages = new ConcurrentBag<RecordedMessage>();
        var eventLog = new EventLog(20);

        var client = new BeatDashEventClient(settings.Host);

        client.OnMapEvent += (_, raw) => messages.Add(new RecordedMessage(DateTimeOffset.UtcNow, "MapData", raw));
        client.OnLiveEvent += (_, raw) => messages.Add(new RecordedMessage(DateTimeOffset.UtcNow, "LiveData", raw));

        client.OnConnectionStateChanged += (_, args) => {
            var color = args.Status switch {
                ConnectionStatus.Connected => "green",
                ConnectionStatus.Connecting => "yellow",
                ConnectionStatus.Reconnecting => "yellow",
                _ => "red"
            };
            eventLog.Add($"[{color}]{args.EndpointName}[/] connection: [{color}]{args.Status}[/]");
        };

        client.OnMapStarted += (_, args) => {
            var s = args.Session;
            eventLog.Add($"[green]MapStarted[/] {s.SongName.EscapeMarkup()} ({s.Difficulty.EscapeMarkup()}) by {s.SongAuthor.EscapeMarkup()}");
        };

        client.OnMapFinished += (_, args) => {
            var s = args.Session;
            eventLog.Add(
                $"[green]MapFinished[/] {s.SongName.EscapeMarkup()} | Score: {s.FinalScore:N0} | Acc: {s.FinalAccuracy:F2}% | Rank: {s.FinalRank.EscapeMarkup()}");
        };

        client.OnMapSuccess += (_, args) => {
            var s = args.Session;
            var fc = s.FullCombo ? " | [green]FC[/]" : $" | {s.FinalMisses} miss";
            eventLog.Add($"[green]MapSuccess[/] {s.SongName.EscapeMarkup()}{fc} | {s.FinalScore:N0}");
        };

        client.OnMapFailed += (_, args) => {
            var s = args.Session;
            eventLog.Add($"[red]MapFailed[/] {s.SongName.EscapeMarkup()} | Score: {s.FinalScore:N0} | Acc: {s.FinalAccuracy:F2}%");
        };

        client.OnMapEnded += (_, args) => {
            var s = args.Session;
            eventLog.Add($"[yellow]MapEnded[/] {s.SongName.EscapeMarkup()} quit at {s.DurationPlayed}s / {s.Duration}s");
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Console.CancelKeyPress += (_, e) => {
            e.Cancel = true;
            cts.Cancel();
        };

        var connectTask = client.ConnectAsync(cts.Token);

        var display = new RecorderDisplay(client, eventLog, outFile);

        await AnsiConsole.Live(display)
            .StartAsync(async ctx => {
                while (!cts.IsCancellationRequested) {
                    ctx.Refresh();
                    try {
                        await Task.Delay(250, cts.Token);
                    } catch (OperationCanceledException) {
                        break;
                    }
                }
                ctx.Refresh();
            });

        cts.Cancel();

        try {
            await connectTask;
        } catch (OperationCanceledException) { }

        var sorted = messages.OrderBy(m => m.Timestamp).ToList();
        var json = JsonSerializer.Serialize(sorted, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(outFile, json, CancellationToken.None);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]Recording saved to [white]{outFile.EscapeMarkup()}[/] ({sorted.Count} messages)[/]");

        return 0;
    }
}

file class EventLog(int maxEntries) {
    private readonly List<(DateTimeOffset Time, string Markup)> _entries = [];
    private readonly object _lock = new();

    public void Add(string markup) {
        lock (_lock) {
            _entries.Add((DateTimeOffset.UtcNow, markup));
            while (_entries.Count > maxEntries)
                _entries.RemoveAt(0);
        }
    }

    public IReadOnlyList<(DateTimeOffset Time, string Markup)> GetEntries() {
        lock (_lock) {
            return _entries.ToList().AsReadOnly();
        }
    }
}

file class RecorderDisplay(BeatDashEventClient client, EventLog eventLog, string outFile) : IRenderable {
    public Measurement Measure(RenderOptions options, int maxWidth) {
        return BuildContent().Measure(options, maxWidth);
    }

    public IEnumerable<Segment> Render(RenderOptions options, int maxWidth) {
        return BuildContent().Render(options, maxWidth);
    }

    private IRenderable BuildContent() {
        var content = new List<IRenderable> {
            BuildConnectionsSection(),
            new Rule().DoubleBorder(),
            BuildMapStatusSection(),
            new Rule().DoubleBorder(),
            BuildEventLogSection(),
            new Rule().DoubleBorder(),
            new Markup($"  [grey]{outFile.EscapeMarkup()}[/]")
        };

        return new Panel(new Rows(content))
            .Header(" [bold]BeatDash Recorder[/] ")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Grey);
    }

    private IRenderable BuildConnectionsSection() {
        var now = DateTimeOffset.UtcNow;
        var rows = new List<IRenderable> {
            new Markup("  [bold]Connections[/]")
        };

        foreach (var ep in client.Endpoints.Values.OrderBy(e => e.Name)) {
            var (icon, label) = ep.Status switch {
                ConnectionStatus.Connected => ("[green]●[/]", "Connected"),
                ConnectionStatus.Connecting => ("[yellow]◐[/]", "Connecting..."),
                ConnectionStatus.Reconnecting => ("[yellow]↻[/]", "Reconnecting"),
                _ => ("[red]○[/]", "Disconnected")
            };

            var lastMsg = ep.LastMessageTime.HasValue
                ? FormatElapsedTime(now - ep.LastMessageTime.Value)
                : "-";

            rows.Add(new Markup(
                $"    {icon} [bold]{ep.Name.EscapeMarkup()}[/]  [grey]{label} | {ep.MessageCount} events | {lastMsg.EscapeMarkup()}[/]"
            ));
        }

        return new Rows(rows);
    }

    private IRenderable BuildMapStatusSection() {
        var session = client.CurrentSession;

        if (session == null) {
            return new Rows(
                new Markup("  [bold grey]■ Idle[/]"),
                new Markup("  [grey]Waiting for a map to start...[/]")
            );
        }

        var lastSnapshot = session.Snapshots.LastOrDefault();

        var statusLabel = session.IsPaused ? "[bold yellow]⏸ Paused[/]" : "[bold green]▶ Playing[/]";
        var elapsed = FormatDuration(lastSnapshot?.TimeElapsed ?? 0);
        var total = FormatDuration(session.Duration);
        var noFail = session.NoFailEnabled ? " | [yellow]NoFail[/]" : "";
        var bsr = !string.IsNullOrEmpty(session.BSRKey) ? $" | BSR: {session.BSRKey.EscapeMarkup()}" : "";
        var score = lastSnapshot?.Score ?? 0;
        var rank = lastSnapshot?.Rank ?? "SSS";
        var combo = lastSnapshot?.Combo ?? 0;
        var misses = lastSnapshot?.Misses ?? 0;
        var fullCombo = lastSnapshot?.FullCombo ?? true;
        var accuracy = lastSnapshot?.Accuracy ?? 100;
        var fc = fullCombo ? "[green]FC[/]" : $"{misses} miss";

        return new Rows(
            new Markup($"  {statusLabel}"),
            new Markup($"  [bold white]{session.SongName.EscapeMarkup()}[/] — [grey]{session.SongAuthor.EscapeMarkup()}[/]"),
            new Markup(
                $"  [grey]{session.Difficulty.EscapeMarkup()} | {session.MapType.EscapeMarkup()} | BPM {session.BPM} | NJS {session.NJS:F1}{noFail}{bsr}[/]"),
            new Markup($"  {elapsed} / {total}"),
            new Markup($"  Score: [bold]{score:N0}[/] ({rank.EscapeMarkup()}) | Combo: {combo} | {fc} | Acc: {accuracy:F2}%")
        );
    }

    private static string FormatDuration(int seconds) {
        var span = TimeSpan.FromSeconds(seconds);
        return $"{(int) span.TotalMinutes}:{span.Seconds:D2}";
    }

    private static string FormatElapsedTime(TimeSpan elapsed) {
        if (elapsed.TotalSeconds < 1) return "just now";
        if (elapsed.TotalMinutes < 1) return $"{(int) elapsed.TotalSeconds}s ago";
        return $"{(int) elapsed.TotalMinutes}m ago";
    }

    private IRenderable BuildEventLogSection() {
        var entries = eventLog.GetEntries();
        var rows = new List<IRenderable> {
            new Markup("  [bold]Event Log[/]")
        };

        if (entries.Count == 0) {
            rows.Add(new Markup("  [grey]No events yet...[/]"));
            return new Rows(rows);
        }

        foreach (var (time, markup) in entries) {
            rows.Add(new Markup($"  [grey]{time:HH:mm:ss}[/] {markup}"));
        }

        return new Rows(rows);
    }
}
