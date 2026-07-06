using System.ComponentModel;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Shiron.BeatDash.Beatmaps;
using Shiron.BeatDash.Data.Socket;
using SkiaSharp;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shiron.BeatDash.Cli.Commands;

/// <summary>
/// Bulk-imports a CustomLevels directory into the server by parsing each map locally and
/// posting it to the admin <c>/api/maps/import</c> endpoint — the persistence half of a
/// map-start, one call per (map, difficulty). Achieves the same DB result as starting each
/// map in Beat Saber, without a play session.
/// </summary>
public sealed class PushMapsCommand : AsyncCommand<PushMapsCommand.Settings> {
    public sealed class Settings : CommandSettings {
        [CommandArgument(0, "<root>")]
        [Description("A CustomLevels directory, or a single map folder containing Info.dat")]
        public string Root { get; init; } = "";

        [CommandOption("--host")]
        [Description("Base URL of the BeatDash API")]
        public string Host { get; init; } = "http://localhost:1811";

        [CommandOption("--email")]
        [Description("Admin email (default: admin@beatdash.local or $AdminUser__Email)")]
        public string? Email { get; init; }

        [CommandOption("--password")]
        [Description("Admin password (default: admin or $AdminUser__Password)")]
        public string? Password { get; init; }

        [CommandOption("--dry-run")]
        [Description("Parse and report, but do not post anything")]
        public bool DryRun { get; init; }
    }

    private static readonly string[] CoverExtensions = [".png", ".jpg", ".jpeg"];

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken ct) {
        if (!Directory.Exists(settings.Root)) {
            AnsiConsole.MarkupLineInterpolated($"[red]Directory not found:[/] {settings.Root}");
            return 1;
        }

        var folders = ResolveMapFolders(settings.Root);
        if (folders.Count == 0) {
            AnsiConsole.MarkupLine("[yellow]No map folders found.[/]");
            return 0;
        }

        AnsiConsole.MarkupLineInterpolated($"[grey]Found[/] {folders.Count} [grey]map folder(s)[/]");

        using var http = CreateClient(settings.Host);
        if (!settings.DryRun && !await LoginAsync(http, settings, ct)) return 1;

        var maps = 0;
        var difficulties = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var folder in folders) {
            ct.ThrowIfCancellationRequested();
            var name = Path.GetFileName(folder);

            ParsedLevel level;
            string levelId;
            byte[] cover;
            try {
                var source = new DirectoryBeatmapFileSource(folder);
                level = BeatmapParser.ParseLevel(source);
                levelId = SongCoreHash.LevelId(source);
                var coverBytes = ReadCover(folder);
                if (coverBytes is null) {
                    AnsiConsole.MarkupLineInterpolated($"[yellow]skip[/] {Markup.Escape(name)} [grey](no cover image)[/]");
                    skipped++;
                    continue;
                }
                cover = coverBytes;
            } catch (Exception ex) {
                AnsiConsole.MarkupLineInterpolated($"[red]skip[/] {Markup.Escape(name)} [grey]({Markup.Escape(ex.Message)})[/]");
                skipped++;
                continue;
            }

            if (level.Beatmaps.Count == 0) {
                skipped++;
                continue;
            }

            maps++;
            var durationMs = EstimateDurationMs(level);
            var pushedForMap = 0;

            // Sequential per map: the first difficulty creates the beatmap (and fires the
            // fetch); the rest attach to it — avoids a dedup race on a new beatmap.
            foreach (var beatmap in level.Beatmaps) {
                var message = BuildMessage(level, beatmap, levelId, durationMs);
                if (settings.DryRun) {
                    difficulties++;
                    pushedForMap++;
                    continue;
                }

                if (await ImportAsync(http, message, cover, ct)) {
                    difficulties++;
                    pushedForMap++;
                } else {
                    failed++;
                }
            }

            AnsiConsole.MarkupLineInterpolated(
                $"[green]push[/] {Markup.Escape(name)} [grey]— {pushedForMap} difficulty(ies), {levelId[..Math.Min(levelId.Length, 25)]}…[/]");
        }

        AnsiConsole.WriteLine();
        var dry = settings.DryRun ? " [yellow](dry-run, nothing posted)[/]" : "";
        AnsiConsole.MarkupLine(
            $"[bold]Done[/] — maps: {maps}, difficulties: {difficulties}, skipped: {skipped}, failed: {failed}{dry}");
        return failed > 0 ? 1 : 0;
    }

    private static List<string> ResolveMapFolders(string root) {
        // A single map folder (has Info.dat) vs a directory of map folders.
        if (Directory.EnumerateFiles(root).Any(f => string.Equals(Path.GetFileName(f), "Info.dat", StringComparison.OrdinalIgnoreCase))) {
            return [root];
        }
        return Directory.EnumerateDirectories(root).OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    private static HttpClient CreateClient(string host) {
        var handler = new HttpClientHandler { CookieContainer = new CookieContainer(), UseCookies = true };
        return new HttpClient(handler) { BaseAddress = new Uri(host) };
    }

    private static async Task<bool> LoginAsync(HttpClient http, Settings settings, CancellationToken ct) {
        var email = settings.Email ?? Environment.GetEnvironmentVariable("AdminUser__Email") ?? "admin@beatdash.local";
        var password = settings.Password ?? Environment.GetEnvironmentVariable("AdminUser__Password") ?? "admin";

        try {
            var resp = await http.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password, RememberMe = true }, ct);
            if (!resp.IsSuccessStatusCode) {
                AnsiConsole.MarkupLineInterpolated($"[red]Login failed[/] ({(int) resp.StatusCode}) for {Markup.Escape(email)}");
                return false;
            }
            AnsiConsole.MarkupLineInterpolated($"[grey]Authenticated as[/] {Markup.Escape(email)}");
            return true;
        } catch (HttpRequestException ex) {
            AnsiConsole.MarkupLineInterpolated($"[red]Cannot reach {Markup.Escape(settings.Host)}:[/] {Markup.Escape(ex.Message)}");
            return false;
        }
    }

    private static async Task<bool> ImportAsync(HttpClient http, MapStartMessage message, byte[] cover, CancellationToken ct) {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(JsonSerializer.Serialize(message)), "metadata");
        var coverContent = new ByteArrayContent(cover);
        coverContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(coverContent, "cover", "cover.png");

        try {
            var resp = await http.PostAsync("/api/maps/import", form, ct);
            if (resp.IsSuccessStatusCode) return true;
            var body = await resp.Content.ReadAsStringAsync(ct);
            AnsiConsole.MarkupLineInterpolated(
                $"[red]import failed[/] ({(int) resp.StatusCode}) {Markup.Escape(message.Difficulty)}: {Markup.Escape(body[..Math.Min(body.Length, 120)])}");
            return false;
        } catch (HttpRequestException ex) {
            AnsiConsole.MarkupLineInterpolated($"[red]import error[/]: {Markup.Escape(ex.Message)}");
            return false;
        }
    }

    private static MapStartMessage BuildMessage(ParsedLevel level, ParsedBeatmap beatmap, string levelId, int durationMs) {
        var durationSeconds = durationMs / 1000.0;
        var left = beatmap.Notes.Count(n => n.Color == 0);
        var right = beatmap.Notes.Count(n => n.Color == 1);
        var serialized = beatmap.Characteristic;

        return new MapStartMessage {
            CorrelationId = 0,
            LevelId = levelId,
            DurationMs = durationMs,
            NotesPerSecond = durationSeconds > 0 ? (float) (beatmap.Notes.Count / durationSeconds) : 0f,
            SongName = level.SongName,
            SongSubName = level.SongSubName,
            SongAuthor = level.SongAuthor,
            Mapper = string.IsNullOrEmpty(level.Mapper) ? level.SongAuthor : level.Mapper,
            Bpm = (float) level.Bpm,
            Difficulty = beatmap.Difficulty,
            DifficultyName = beatmap.Difficulty,
            NoteJumpSpeed = beatmap.Njs is { } njs ? (float) njs : null,
            BombCount = beatmap.Bombs.Count,
            ObstacleCount = beatmap.Obstacles.Count,
            CuttableObjectCount = beatmap.Notes.Count,
            LaneCount = 4,
            Characteristic = new BeatmapCharacteristic {
                SerializedName = serialized,
                ColorCount = serialized == "OneSaber" ? 1 : 2,
                Requires360Movement = serialized == "360Degree",
                ContainsRotationEvents = serialized is "360Degree" or "90Degree",
                LocalizationKey = "",
                DescriptionLocalizationKey = "",
            },
            AutoMode = false,
            ModifierFlags = 0,
            SongSpeed = 1f,
            NotesPerHandLeft = left,
            NotesPerHandRight = right,
            NpsCurve = [],
            WallTimeline = [],
            BombPositions = [],
        };
    }

    /// <summary>Approximates the song length from the last object across all difficulties.</summary>
    private static int EstimateDurationMs(ParsedLevel level) {
        if (level.Bpm <= 0) return 0;

        var lastBeat = 0.0;
        foreach (var bm in level.Beatmaps) {
            foreach (var n in bm.Notes) lastBeat = Math.Max(lastBeat, n.Beat);
            foreach (var b in bm.Bombs) lastBeat = Math.Max(lastBeat, b.Beat);
            foreach (var o in bm.Obstacles) lastBeat = Math.Max(lastBeat, o.Beat + o.Duration);
            foreach (var c in bm.Chains) lastBeat = Math.Max(lastBeat, c.TailBeat);
            foreach (var a in bm.Arcs) lastBeat = Math.Max(lastBeat, a.TailBeat);
        }

        return (int) (lastBeat / level.Bpm * 60.0 * 1000.0);
    }

    /// <summary>Reads the folder's cover image and transcodes it to PNG. Null if none found.</summary>
    private static byte[]? ReadCover(string folder) {
        var coverPath = Directory.EnumerateFiles(folder)
            .Where(f => CoverExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderByDescending(f => Path.GetFileNameWithoutExtension(f).Equals("cover", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();
        if (coverPath is null) return null;

        var bytes = File.ReadAllBytes(coverPath);
        if (Path.GetExtension(coverPath).Equals(".png", StringComparison.OrdinalIgnoreCase)) return bytes;

        try {
            using var bitmap = SKBitmap.Decode(bytes);
            if (bitmap is null) return bytes; // let the server take the raw bytes as a fallback
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        } catch {
            return bytes;
        }
    }
}
