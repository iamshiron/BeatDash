using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using Shiron.BeatDash.API.Data.Entities;
using Shiron.BeatDash.API.Models;

namespace Shiron.BeatDash.API.Data;

public interface IDatabaseService {
    Task InitializeAsync();
    Task<PlaySessionEntity> CreatePlaySessionAsync(MapData mapData);
    Task UpdatePlaySessionFinalDataAsync(long playSessionId, MapData mapData, LiveData? liveData);
    Task UpdateMapCoverImageAsync(string hash, string? coverImage);
    Task<LiveDataSnapshotEntity> AddLiveDataSnapshotAsync(long playSessionId, LiveData liveData);
    Task<RawMessageEntity> AddRawMessageAsync(string connectionName, string message, long? playSessionId = null);
    Task<PlaySessionEntity?> GetCurrentPlaySessionAsync();
    IQueryable<PlaySessionEntity> QueryPlaySessions();
    IQueryable<LiveDataSnapshotEntity> QueryLiveDataSnapshots();
    IQueryable<RawMessageEntity> QueryRawMessages();
}

public class DatabaseService : IDatabaseService {
    private readonly BeatDashDbContext _context;
    private readonly ILogger<DatabaseService> _logger;
    private readonly HttpClient _httpClient;
    private long? _currentPlaySessionId;

    public DatabaseService(BeatDashDbContext context, ILogger<DatabaseService> logger, HttpClient httpClient) {
        _context = context;
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task InitializeAsync() {
        _logger.LogInformation("Initializing PostgreSQL database...");
        await _context.Database.MigrateAsync();
        _logger.LogInformation("PostgreSQL database initialized successfully");
    }

    public async Task<PlaySessionEntity> CreatePlaySessionAsync(MapData mapData) {
        var map = await GetOrCreateMapAsync(mapData);
        var difficulty = await GetOrCreateDifficultyAsync(mapData, map.Id);

        var session = new PlaySessionEntity {
            StartedAt = DateTimeOffset.UtcNow,
            MapId = map.Id,
            DifficultyId = difficulty.Id,
            ModifiersMultiplier = mapData.ModifiersMultiplier,
            PracticeMode = mapData.PracticeMode,
            PluginVersion = mapData.PluginVersion,
            IsMultiplayer = mapData.IsMultiplayer,
            PreviousRecord = mapData.PreviousRecord,
            PreviousBSR = mapData.PreviousBSR,
            Modifiers = MapModifiers(mapData.Modifiers),
            PracticeModeModifiers = MapPracticeModeModifiers(mapData.PracticeModeModifiers),
        };

        _context.PlaySessions.Add(session);
        await _context.SaveChangesAsync();

        _currentPlaySessionId = session.Id;
        _logger.LogDebug("Created play session {Id} for {SongName}", session.Id, map.SongName);

        return session;
    }

    private async Task<MapEntity> GetOrCreateMapAsync(MapData mapData) {
        var hash = mapData.Hash ?? "";
        var existingMap = await _context.Maps.FirstOrDefaultAsync(m => m.Hash == hash);
        var coverImage = await ConvertCoverImageToWebPAsync(mapData.CoverImage);

        if (existingMap != null) {
            if (existingMap.CoverImage == null && coverImage != null) {
                existingMap.CoverImage = coverImage;
                await _context.SaveChangesAsync();
            }
            return existingMap;
        }

        var map = new MapEntity {
            Hash = hash,
            SongName = mapData.SongName,
            SongSubName = mapData.SongSubName,
            SongAuthor = mapData.SongAuthor,
            Mapper = mapData.Mapper,
            BSRKey = mapData.BSRKey,
            Duration = mapData.Duration,
            BPM = mapData.BPM,
            CoverImage = coverImage,
            GameVersion = mapData.GameVersion,
        };

        _context.Maps.Add(map);
        await _context.SaveChangesAsync();
        return map;
    }

    private static readonly Regex DataUriPattern = new(@"^data:image/\w+;base64,(.+)$", RegexOptions.Compiled);

    private async Task<string?> ConvertCoverImageToWebPAsync(string? coverImage) {
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
                imageBytes = await _httpClient.GetByteArrayAsync(uri);
            } catch {
                _logger.LogWarning("Failed to download cover image from URL: {Url}", coverImage);
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

    private async Task<DifficultyEntity> GetOrCreateDifficultyAsync(MapData mapData, long mapId) {
        var existingDifficulty = await _context.Difficulties.FirstOrDefaultAsync(
            d => d.MapId == mapId && d.MapType == mapData.MapType && d.Difficulty == mapData.Difficulty);
        if (existingDifficulty != null) return existingDifficulty;

        var difficulty = new DifficultyEntity {
            MapId = mapId,
            MapType = mapData.MapType,
            Difficulty = mapData.Difficulty,
            CustomDifficultyLabel = mapData.CustomDifficultyLabel,
            NJS = mapData.NJS,
            PP = mapData.PP,
            Star = mapData.Star,
        };

        _context.Difficulties.Add(difficulty);
        await _context.SaveChangesAsync();
        return difficulty;
    }

    public async Task UpdatePlaySessionFinalDataAsync(long playSessionId, MapData mapData, LiveData? liveData) {
        var session = await _context.PlaySessions.FindAsync(playSessionId);
        if (session == null) {
            _logger.LogWarning("Play session {Id} not found for update", playSessionId);
            return;
        }

        session.FinishedAt = DateTimeOffset.UtcNow;
        session.EndReason = mapData.LevelFinished ? "Finished" : mapData.LevelFailed ? "Failed" : "Quit";

        if (liveData != null) {
            session.FinalScore = liveData.Score;
            session.FinalScoreWithMultipliers = liveData.ScoreWithMultipliers;
            session.FinalMaxScore = liveData.MaxScore;
            session.FinalMaxScoreWithMultipliers = liveData.MaxScoreWithMultipliers;
            session.FinalRank = liveData.Rank;
            session.FinalFullCombo = liveData.FullCombo;
            session.FinalCombo = liveData.Combo;
            session.FinalMisses = liveData.Misses;
            session.FinalAccuracy = liveData.Accuracy;
            session.FinalTimeElapsed = liveData.TimeElapsed;
        }

        await _context.SaveChangesAsync();
        _currentPlaySessionId = null;

        _logger.LogDebug("Updated play session {Id} with final data", playSessionId);
    }

    public async Task UpdateMapCoverImageAsync(string hash, string? coverImage) {
        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(coverImage)) return;

        var map = await _context.Maps.FirstOrDefaultAsync(m => m.Hash == hash);
        if (map == null || map.CoverImage != null) return;

        var webpCoverImage = await ConvertCoverImageToWebPAsync(coverImage);
        if (webpCoverImage == null) return;

        map.CoverImage = webpCoverImage;
        await _context.SaveChangesAsync();
        _logger.LogDebug("Updated cover image for map {Hash}", hash);
    }

    public async Task<LiveDataSnapshotEntity> AddLiveDataSnapshotAsync(long playSessionId, LiveData liveData) {
        var snapshot = new LiveDataSnapshotEntity {
            Timestamp = DateTimeOffset.UtcNow,
            PlaySessionId = playSessionId,
            Score = liveData.Score,
            ScoreWithMultipliers = liveData.ScoreWithMultipliers,
            MaxScore = liveData.MaxScore,
            MaxScoreWithMultipliers = liveData.MaxScoreWithMultipliers,
            Rank = liveData.Rank,
            FullCombo = liveData.FullCombo,
            NotesSpawned = liveData.NotesSpawned,
            Combo = liveData.Combo,
            Misses = liveData.Misses,
            Accuracy = liveData.Accuracy,
            PlayerHealth = liveData.PlayerHealth,
            TimeElapsed = liveData.TimeElapsed,
            EventTrigger = (int) liveData.EventTrigger,
            BlockHitPreSwing = liveData.BlockHitScore.PreSwing,
            BlockHitPostSwing = liveData.BlockHitScore.PostSwing,
            BlockHitCenterSwing = liveData.BlockHitScore.CenterSwing,
            NoteColorType = (int) liveData.ColorType,
        };

        _context.LiveDataSnapshots.Add(snapshot);
        await _context.SaveChangesAsync();

        return snapshot;
    }

    public async Task<RawMessageEntity> AddRawMessageAsync(string connectionName, string message, long? playSessionId = null) {
        var rawMessage = new RawMessageEntity {
            Timestamp = DateTimeOffset.UtcNow,
            ConnectionName = connectionName,
            Message = message,
            PlaySessionId = playSessionId ?? _currentPlaySessionId,
        };

        _context.RawMessages.Add(rawMessage);
        await _context.SaveChangesAsync();

        return rawMessage;
    }

    public async Task<PlaySessionEntity?> GetCurrentPlaySessionAsync() {
        if (_currentPlaySessionId == null) return null;
        return await _context.PlaySessions.FindAsync(_currentPlaySessionId.Value);
    }

    public IQueryable<PlaySessionEntity> QueryPlaySessions() => _context.PlaySessions.AsQueryable();
    public IQueryable<LiveDataSnapshotEntity> QueryLiveDataSnapshots() => _context.LiveDataSnapshots.AsQueryable();
    public IQueryable<RawMessageEntity> QueryRawMessages() => _context.RawMessages.AsQueryable();

    private static ModifiersEntity MapModifiers(Modifiers modifiers) => new() {
        NoFailOn0Energy = modifiers.NoFailOn0Energy,
        OneLife = modifiers.OneLife,
        FourLives = modifiers.FourLives,
        NoBombs = modifiers.NoBombs,
        NoWalls = modifiers.NoWalls,
        NoArrows = modifiers.NoArrows,
        GhostNotes = modifiers.GhostNotes,
        DisappearingArrows = modifiers.DisappearingArrows,
        SmallNotes = modifiers.SmallNotes,
        ProMode = modifiers.ProMode,
        StrictAngles = modifiers.StrictAngles,
        ZenMode = modifiers.ZenMode,
        SlowerSong = modifiers.SlowerSong,
        FasterSong = modifiers.FasterSong,
        SuperFastSong = modifiers.SuperFastSong,
    };

    private static PracticeModeModifiersEntity MapPracticeModeModifiers(PracticeModeModifiers modifiers) => new() {
        SongSpeedMul = modifiers.SongSpeedMul,
        StartInAdvanceAndClearNotes = modifiers.StartInAdvanceAndClearNotes,
        SongStartTime = modifiers.SongStartTime,
    };
}
