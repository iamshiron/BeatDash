using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using Shiron.BeatDash.API.Data;
using Shiron.BeatDash.API.Data.Entities;
using Shiron.BeatDash.Data.Models;

namespace Shiron.BeatDash.API.Endpoints;

public static class RecordingEndpoints {
    private static readonly Regex DataUriPattern = new(@"^data:image/\w+;base64,(.+)$", RegexOptions.Compiled);

    public static void MapRecordingEndpoints(this IEndpointRouteBuilder endpoints) {
        var router = endpoints.MapGroup("/recordings");

        router.MapPost("/upload", async (HttpRequest request, BeatDashDbContext db, IHttpClientFactory httpFactory, CancellationToken ct) => {
            var data = await JsonSerializer.DeserializeAsync<List<RecordedMessage>>(request.Body, cancellationToken: ct);
            if (data == null || data.Count == 0) {
                return Results.BadRequest(new { Error = "No data provided" });
            }

            var httpClient = httpFactory.CreateClient();
            var parsed = ParseSessions(data);

            var sessionCount = 0;
            var snapshotCount = 0;

            foreach (var session in parsed) {
                var map = await GetOrCreateMapAsync(db, session, httpClient, ct);
                var difficulty = await GetOrCreateDifficultyAsync(db, session, map.Id, ct);

                var playSession = new PlaySessionEntity {
                    StartedAt = session.StartedAt,
                    FinishedAt = session.EndedAt,
                    EndReason = session.EndReason?.ToString(),
                    MapId = map.Id,
                    DifficultyId = difficulty.Id,
                    ModifiersMultiplier = session.ModifiersMultiplier,
                    PracticeMode = session.PracticeMode,
                    PluginVersion = session.PluginVersion,
                    IsMultiplayer = session.IsMultiplayer,
                    PreviousRecord = session.PreviousRecord,
                    PreviousBSR = session.PreviousBSR,
                    Modifiers = MapModifiers(session.Modifiers),
                    PracticeModeModifiers = MapPracticeModeModifiers(session.PracticeModeModifiers),
                };

                db.PlaySessions.Add(playSession);
                await db.SaveChangesAsync(ct);
                sessionCount++;

                foreach (var snapshot in session.Snapshots) {
                    db.LiveDataSnapshots.Add(new LiveDataSnapshotEntity {
                        Timestamp = snapshot.Timestamp,
                        PlaySessionId = playSession.Id,
                        Score = snapshot.Score,
                        ScoreWithMultipliers = snapshot.ScoreWithMultipliers,
                        MaxScore = snapshot.MaxScore,
                        MaxScoreWithMultipliers = snapshot.MaxScoreWithMultipliers,
                        Rank = snapshot.Rank,
                        FullCombo = snapshot.FullCombo,
                        NotesSpawned = snapshot.NotesSpawned,
                        Combo = snapshot.Combo,
                        Misses = snapshot.Misses,
                        Accuracy = snapshot.Accuracy,
                        PlayerHealth = snapshot.PlayerHealth,
                        TimeElapsed = snapshot.TimeElapsed,
                        EventTrigger = 0,
                        BlockHitPreSwing = snapshot.BlockHitScore.PreSwing,
                        BlockHitPostSwing = snapshot.BlockHitScore.PostSwing,
                        BlockHitCenterSwing = snapshot.BlockHitScore.CenterSwing,
                        NoteColorType = -1,
                    });
                    snapshotCount++;
                }

                await db.SaveChangesAsync(ct);
            }

            return Results.Ok(new { Sessions = sessionCount, Snapshots = snapshotCount });
        }).DisableAntiforgery().WithMetadata(new DisableRequestSizeLimitAttribute());
    }

    private static List<MapSessionData> ParseSessions(List<RecordedMessage> data) {
        var sessions = new List<MapSessionData>();
        MapSessionData? current = null;

        foreach (var message in data) {
            switch (message.Endpoint) {
                case "MapData":
                    var mapData = JsonSerializer.Deserialize<MapData>(message.Message);
                    if (mapData?.Hash == null) continue;

                    var isEndSignal = mapData.LevelFinished || mapData.LevelQuit
                        || (mapData.LevelFailed && !mapData.Modifiers.NoFailOn0Energy);

                    if (isEndSignal) {
                        if (current != null) {
                            current.EndedAt = message.Timestamp;
                            current.FullCleared = mapData.LevelFinished;
                            current.EndReason = mapData.LevelFinished ? MapEndReason.Finished
                                : mapData.LevelQuit ? MapEndReason.Quit
                                : MapEndReason.Failed;
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
                            NoFailEnabled = mapData.Modifiers.NoFailOn0Energy,
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
                        BlockHitScore = liveData.BlockHitScore,
                    });

                    continue;

                default:
                    continue;
            }
        }

        if (current != null) {
            current.EndedAt = data.Last().Timestamp;
            sessions.Add(current);
        }

        return sessions;
    }

    private static async Task<MapEntity> GetOrCreateMapAsync(
        BeatDashDbContext db, MapSessionData session, HttpClient httpClient, CancellationToken ct) {
        var hash = session.Hash ?? "";
        var existing = await db.Maps.FirstOrDefaultAsync(m => m.Hash == hash, ct);

        if (existing != null) {
            if (existing.CoverImage == null && session.CoverImage != null) {
                existing.CoverImage = await ConvertCoverImageToWebPAsync(httpClient, session.CoverImage);
                await db.SaveChangesAsync(ct);
            }
            return existing;
        }

        var map = new MapEntity {
            Hash = hash,
            SongName = session.SongName,
            SongSubName = session.SongSubName,
            SongAuthor = session.SongAuthor,
            Mapper = session.Mapper,
            BSRKey = session.BSRKey,
            Duration = session.Duration,
            BPM = session.BPM,
            CoverImage = await ConvertCoverImageToWebPAsync(httpClient, session.CoverImage),
            GameVersion = session.GameVersion,
        };

        db.Maps.Add(map);
        await db.SaveChangesAsync(ct);
        return map;
    }

    private static async Task<DifficultyEntity> GetOrCreateDifficultyAsync(
        BeatDashDbContext db, MapSessionData session, long mapId, CancellationToken ct) {
        var existing = await db.Difficulties.FirstOrDefaultAsync(
            d => d.MapId == mapId && d.MapType == session.MapType && d.Difficulty == session.Difficulty, ct);
        if (existing != null) return existing;

        var difficulty = new DifficultyEntity {
            MapId = mapId,
            MapType = session.MapType,
            Difficulty = session.Difficulty,
            CustomDifficultyLabel = session.CustomDifficultyLabel,
            NJS = session.NJS,
            PP = session.PP,
            Star = session.Star,
        };

        db.Difficulties.Add(difficulty);
        await db.SaveChangesAsync(ct);
        return difficulty;
    }

    private static async Task<string?> ConvertCoverImageToWebPAsync(HttpClient httpClient, string? coverImage) {
        if (string.IsNullOrEmpty(coverImage)) return null;

        byte[] imageBytes;

        var match = DataUriPattern.Match(coverImage);
        if (match.Success) {
            try {
                imageBytes = Convert.FromBase64String(match.Groups[1].Value);
            } catch {
                return coverImage;
            }
        } else if (Uri.TryCreate(coverImage, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)) {
            try {
                imageBytes = await httpClient.GetByteArrayAsync(uri);
            } catch {
                return null;
            }
        } else {
            return coverImage;
        }

        try {
            using var image = Image.Load(imageBytes);
            using var outputStream = new MemoryStream();
            image.Save(outputStream, new WebpEncoder { Quality = 85 });
            var webpBase64 = Convert.ToBase64String(outputStream.ToArray());
            return $"data:image/webp;base64,{webpBase64}";
        } catch {
            return coverImage;
        }
    }

    private static ModifiersEntity MapModifiers(Modifiers m) => new() {
        NoFailOn0Energy = m.NoFailOn0Energy,
        OneLife = m.OneLife,
        FourLives = m.FourLives,
        NoBombs = m.NoBombs,
        NoWalls = m.NoWalls,
        NoArrows = m.NoArrows,
        GhostNotes = m.GhostNotes,
        DisappearingArrows = m.DisappearingArrows,
        SmallNotes = m.SmallNotes,
        ProMode = m.ProMode,
        StrictAngles = m.StrictAngles,
        ZenMode = m.ZenMode,
        SlowerSong = m.SlowerSong,
        FasterSong = m.FasterSong,
        SuperFastSong = m.SuperFastSong,
    };

    private static PracticeModeModifiersEntity MapPracticeModeModifiers(PracticeModeModifiers m) => new() {
        SongSpeedMul = m.SongSpeedMul,
        StartInAdvanceAndClearNotes = m.StartInAdvanceAndClearNotes,
        SongStartTime = m.SongStartTime,
    };
}
