using System.Text.Json;
using Shiron.BeatDash.Data.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shiron.BeatDash.CLI.Commands;

public class AnalyzedMapSession {
    public string? Hash { get; set; }
    public string SongName { get; set; } = "";
    public string SongSubName { get; set; } = "";
    public string SongAuthor { get; set; } = "";
    public string Mapper { get; set; } = "";
    public string? BSRKey { get; set; }
    public string? CoverImage { get; set; }
    public int Duration { get; set; }
    public string MapType { get; set; } = "";
    public string Difficulty { get; set; } = "";
    public string? CustomDifficultyLabel { get; set; }
    public int BPM { get; set; }
    public double NJS { get; set; }
    public double PP { get; set; }
    public double Star { get; set; }
    public Modifiers Modifiers { get; set; } = new();
    public float ModifiersMultiplier { get; set; } = 1.0f;
    public bool PracticeMode { get; set; }
    public bool IsMultiplayer { get; set; }

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public double SessionDurationSeconds { get; set; }

    public string? EndReason { get; set; }
    public bool FullCleared { get; set; }

    public int FinalScore { get; set; }
    public int FinalScoreWithMultipliers { get; set; }
    public int MaxScore { get; set; }
    public int MaxScoreWithMultipliers { get; set; }
    public double FinalAccuracy { get; set; }
    public string FinalRank { get; set; } = "";
    public bool FullCombo { get; set; }
    public int MaxCombo { get; set; }
    public int FinalCombo { get; set; }
    public int FinalMisses { get; set; }
    public int NotesSpawned { get; set; }
    public int TotalNotesHit { get; set; }
    public double FinalPlayerHealth { get; set; }
    public double MinHealth { get; set; }
    public double MaxAccuracy { get; set; }
    public double MinAccuracy { get; set; }
    public bool WasFullComboThroughout { get; set; }
    public BlockHitScore? FinalBlockHitScore { get; set; }
    public int DurationPlayed { get; set; }
    public double NotesPerSecond { get; set; }

    public List<LiveDataSnapshot> Snapshots { get; set; } = [];

    public static AnalyzedMapSession From(MapSessionData s) {
        return new AnalyzedMapSession {
            Hash = s.Hash,
            SongName = s.SongName,
            SongSubName = s.SongSubName,
            SongAuthor = s.SongAuthor,
            Mapper = s.Mapper,
            BSRKey = s.BSRKey,
            CoverImage = s.CoverImage,
            Duration = s.Duration,
            MapType = s.MapType,
            Difficulty = s.Difficulty,
            CustomDifficultyLabel = s.CustomDifficultyLabel,
            BPM = s.BPM,
            NJS = s.NJS,
            PP = s.PP,
            Star = s.Star,
            Modifiers = s.Modifiers,
            ModifiersMultiplier = s.ModifiersMultiplier,
            PracticeMode = s.PracticeMode,
            IsMultiplayer = s.IsMultiplayer,
            StartedAt = s.StartedAt,
            EndedAt = s.EndedAt,
            SessionDurationSeconds = s.SessionDuration.TotalSeconds,
            EndReason = s.EndReason?.ToString(),
            FullCleared = s.FullCleared,
            FinalScore = s.FinalScore,
            FinalScoreWithMultipliers = s.FinalScoreWithMultipliers,
            MaxScore = s.MaxScore,
            MaxScoreWithMultipliers = s.MaxScoreWithMultipliers,
            FinalAccuracy = s.FinalAccuracy,
            FinalRank = s.FinalRank,
            FullCombo = s.FullCombo,
            MaxCombo = s.MaxCombo,
            FinalCombo = s.FinalCombo,
            FinalMisses = s.FinalMisses,
            NotesSpawned = s.NotesSpawned,
            TotalNotesHit = s.TotalNotesHit,
            FinalPlayerHealth = s.FinalPlayerHealth,
            MinHealth = s.MinHealth,
            MaxAccuracy = s.MaxAccuracy,
            MinAccuracy = s.MinAccuracy,
            WasFullComboThroughout = s.WasFullComboThroughout,
            FinalBlockHitScore = s.FinalBlockHitScore,
            DurationPlayed = s.DurationPlayed,
            NotesPerSecond = s.SessionDuration.TotalSeconds > 0 ? s.NotesSpawned / s.SessionDuration.TotalSeconds : 0,
            Snapshots = s.Snapshots
        };
    }
}

public class AnalyzeCommand : AsyncCommand<AnalyzeCommand.Settings> {
    public sealed class Settings : CommandSettings {
        [CommandArgument(0, "<file>")] public required string File { get; set; }
    }

    protected async override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken) {
        var file = settings.File;
        if (!File.Exists(file)) throw new FileNotFoundException();

        AnsiConsole.MarkupLine($"[grey]Analyzing file [green]{file}[/][/]...");
        var data = await JsonSerializer.DeserializeAsync<IList<RecordedMessage>>(
            File.OpenRead(file),
            cancellationToken: cancellationToken
        );
        if (data == null || data.Count == 0) throw new Exception("No data found in file");

        AnsiConsole.MarkupLine($"[grey]Found [green]{data.Count}[/] messages[/]");

        var sessions = new List<MapSessionData>();
        MapSessionData? current = null;

        foreach (var message in data) {
            switch (message.Endpoint) {
                case "MapData":
                    var mapData = JsonSerializer.Deserialize<MapData>(message.Message);
                    if (mapData?.Hash == null) continue;

                    var isEndSignal = mapData.LevelFinished || mapData.LevelQuit
                        || mapData.LevelFailed && !mapData.Modifiers.NoFailOn0Energy;

                    if (isEndSignal) {
                        if (current != null) {
                            current.EndedAt = message.Timestamp;
                            current.FullCleared = mapData.LevelFinished;
                            current.EndReason = mapData.LevelFinished ? MapEndReason.Finished
                                : mapData.LevelQuit ? MapEndReason.Quit
                                : MapEndReason.Failed;

                            FinalizeSession(current);

                            sessions.Add(current);
                            current = null;
                        }
                    } else if (current == null) {
                        if (!mapData.InLevel) continue;

                        current = new MapSessionData {
                            Hash = mapData.Hash,
                            SongName = mapData.SongName,
                            SongSubName = mapData.SongSubName,
                            SongAuthor = mapData.SongAuthor,
                            Mapper = mapData.Mapper,
                            BSRKey = mapData.BSRKey,
                            CoverImage = mapData.CoverImage,
                            Duration = mapData.Duration,
                            MapType = mapData.MapType,
                            Difficulty = mapData.Difficulty,
                            CustomDifficultyLabel = mapData.CustomDifficultyLabel,
                            BPM = mapData.BPM,
                            NJS = mapData.NJS,
                            Modifiers = mapData.Modifiers,
                            ModifiersMultiplier = mapData.ModifiersMultiplier,
                            PracticeMode = mapData.PracticeMode,
                            PracticeModeModifiers = mapData.PracticeModeModifiers,
                            PP = mapData.PP,
                            Star = mapData.Star,
                            GameVersion = mapData.GameVersion,
                            PluginVersion = mapData.PluginVersion,
                            IsMultiplayer = mapData.IsMultiplayer,
                            PreviousRecord = mapData.PreviousRecord,
                            PreviousBSR = mapData.PreviousBSR,
                            StartedAt = message.Timestamp,
                            NoFailEnabled = mapData.Modifiers.NoFailOn0Energy
                        };
                    } else {
                        current.IsPaused = mapData.LevelPaused;
                    }

                    continue;

                case "LiveData":
                    var liveData = JsonSerializer.Deserialize<LiveData>(message.Message);
                    if (liveData == null || current == null) continue;

                    current.Snapshots.Add(new LiveDataSnapshot {
                        Timestamp = message.Timestamp,
                        TimeElapsed = liveData.TimeElapsed,
                        Score = liveData.Score,
                        ScoreWithMultipliers = liveData.ScoreWithMultipliers,
                        MaxScore = liveData.MaxScore,
                        MaxScoreWithMultipliers = liveData.MaxScoreWithMultipliers,
                        Accuracy = liveData.Accuracy,
                        PlayerHealth = liveData.PlayerHealth,
                        Combo = liveData.Combo,
                        Misses = liveData.Misses,
                        NotesSpawned = liveData.NotesSpawned,
                        FullCombo = liveData.FullCombo,
                        Rank = liveData.Rank,
                        BlockHitScore = liveData.BlockHitScore
                    });

                    continue;

                default:
                    continue;
            }
        }

        if (current != null) {
            current.EndedAt = data.Last().Timestamp;
            FinalizeSession(current);
            sessions.Add(current);
        }

        var results = sessions.Select(AnalyzedMapSession.From).ToList();

        AnsiConsole.MarkupLine($"[grey]Found [green]{results.Count}[/] map sessions[/]");
        foreach (var s in results) {
            var label = $"[cyan]{Markup.Escape(s.SongName)}[/] by [yellow]{Markup.Escape(s.SongAuthor)}[/]";
            var result = s.EndReason switch {
                "Finished" => "[green]Finished[/]",
                "Failed" => "[red]Failed[/]",
                "Quit" => "[orange3]Quit[/]",
                _ => "[grey]Unknown[/]"
            };
            var duration = $" | {TimeSpan.FromSeconds(s.SessionDurationSeconds):m\\:ss}";
            var score = s.FinalScore > 0 ? $" | Score: [green]{s.FinalScore:N0}[/]" : "";
            var rank = !string.IsNullOrEmpty(s.FinalRank) ? $" | Rank: [bold]{Markup.Escape(s.FinalRank)}[/]" : "";
            var nps = s.NotesPerSecond > 0 ? $" | NPS: [blue]{s.NotesPerSecond:F2}[/]" : "";
            AnsiConsole.MarkupLine($"  {label} - {result}{duration}{score}{rank}{nps}");
        }

        var totalDuration = TimeSpan.FromSeconds(results.Sum(s => s.SessionDurationSeconds));
        AnsiConsole.MarkupLine($"[grey]Total: [green]{totalDuration:hh\\:mm\\:ss}[/] across [green]{results.Count}[/] sessions[/]");

        var json = JsonSerializer.Serialize(results, new JsonSerializerOptions {
            WriteIndented = true
        });
        await File.WriteAllTextAsync("maps.json", json, cancellationToken);

        return 0;
    }

    private static void FinalizeSession(MapSessionData session) {
        var last = session.Snapshots.LastOrDefault();
        if (last == null) return;

        session.FinalScore = last.Score;
        session.FinalScoreWithMultipliers = last.ScoreWithMultipliers;
        session.MaxScore = last.MaxScore;
        session.MaxScoreWithMultipliers = last.MaxScoreWithMultipliers;
        session.FinalAccuracy = last.Accuracy;
        session.FinalRank = last.Rank;
        session.FinalCombo = last.Combo;
        session.FinalMisses = last.Misses;
        session.NotesSpawned = last.NotesSpawned;
        session.FullCombo = last.FullCombo;
        session.FinalPlayerHealth = last.PlayerHealth;
        session.FinalBlockHitScore = last.BlockHitScore;
        session.DurationPlayed = last.TimeElapsed;
    }
}
