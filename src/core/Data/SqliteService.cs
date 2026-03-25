using Microsoft.EntityFrameworkCore;
using Shiron.BeatDash.API.Data.Entities;
using Shiron.BeatDash.API.Models;

namespace Shiron.BeatDash.API.Data;

public interface ISqliteService {
    Task InitializeAsync();
    Task<PlaySessionEntity> CreatePlaySessionAsync(MapData mapData);
    Task UpdatePlaySessionFinalDataAsync(long playSessionId, MapData mapData, LiveData? liveData);
    Task<LiveDataSnapshotEntity> AddLiveDataSnapshotAsync(long playSessionId, LiveData liveData);
    Task<RawMessageEntity> AddRawMessageAsync(string connectionName, string message, long? playSessionId = null);
    Task<PlaySessionEntity?> GetCurrentPlaySessionAsync();
    IQueryable<PlaySessionEntity> QueryPlaySessions();
    IQueryable<LiveDataSnapshotEntity> QueryLiveDataSnapshots();
    IQueryable<RawMessageEntity> QueryRawMessages();
}

public class SqliteService : ISqliteService {
    private readonly BeatDashDbContext _context;
    private readonly ILogger<SqliteService> _logger;
    private long? _currentPlaySessionId;

    public SqliteService(BeatDashDbContext context, ILogger<SqliteService> logger) {
        _context = context;
        _logger = logger;
    }

    public async Task InitializeAsync() {
        _logger.LogInformation("Initializing SQLite database...");
        await _context.Database.EnsureCreatedAsync();
        _logger.LogInformation("SQLite database initialized successfully");
    }

    public async Task<PlaySessionEntity> CreatePlaySessionAsync(MapData mapData) {
        var session = new PlaySessionEntity {
            StartedAt = DateTimeOffset.UtcNow,
            Hash = mapData.Hash,
            SongName = mapData.SongName,
            SongSubName = mapData.SongSubName,
            SongAuthor = mapData.SongAuthor,
            Mapper = mapData.Mapper,
            BSRKey = mapData.BSRKey,
            Duration = mapData.Duration,
            MapType = mapData.MapType,
            Difficulty = mapData.Difficulty,
            CustomDifficultyLabel = mapData.CustomDifficultyLabel,
            BPM = mapData.BPM,
            NJS = mapData.NJS,
            ModifiersMultiplier = mapData.ModifiersMultiplier,
            PracticeMode = mapData.PracticeMode,
            PP = mapData.PP,
            Star = mapData.Star,
            GameVersion = mapData.GameVersion,
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
        _logger.LogDebug("Created play session {Id} for {SongName}", session.Id, session.SongName);

        return session;
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
            EventTrigger = (int)liveData.EventTrigger,
            BlockHitPreSwing = liveData.BlockHitScore.PreSwing,
            BlockHitPostSwing = liveData.BlockHitScore.PostSwing,
            BlockHitCenterSwing = liveData.BlockHitScore.CenterSwing,
            NoteColorType = (int)liveData.ColorType,
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
