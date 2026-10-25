using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shiron.BeatDash.API.Services;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;
using Xunit;

namespace Shiron.BeatDash.API.Tests;

public sealed class WeaknessAggregationServiceTests : IAsyncLifetime {
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "beatdash-weakness-tests", Guid.NewGuid().ToString("N"));
    private DbContextOptions<BeatDashDbContext> _options = null!;

    public async Task InitializeAsync() {
        Directory.CreateDirectory(_directory);
        var options = new DbContextOptionsBuilder<BeatDashDbContext>();
        options.UseBeatDashDatabase(DatabaseProvider.Sqlite, $"Data Source={Path.Combine(_directory, "beatdash.db")};Pooling=False");
        _options = (DbContextOptions<BeatDashDbContext>) options.Options;
        await using var db = new BeatDashDbContext(_options);
        await db.Database.InitializeAsync(applyMigrations: true);
    }

    [Fact]
    public async Task FoldSessionAsync_Sqlite_UpsertsAdditivelyAndOnlyOncePerSession() {
        var (userId, first, second) = await SeedAsync();
        var service = new WeaknessAggregationService(new Factory(_options), NullLogger<WeaknessAggregationService>.Instance);

        await service.FoldSessionAsync(first);
        await service.FoldSessionAsync(first);
        await service.FoldSessionAsync(second);

        await using var db = new BeatDashDbContext(_options);
        var aggregates = await db.PlayNoteAggregates.Where(a => a.UserId == userId).OrderBy(a => a.LineIndex).ToListAsync();
        Assert.Equal(2, aggregates.Count);
        Assert.Equal(2, aggregates[0].NoteCount);
        Assert.Equal(2, aggregates[0].GoodCount);
        Assert.Equal(200, aggregates[0].SumEarnedScore);
        Assert.Equal(230, aggregates[0].SumMaxScore);
        Assert.Equal(1, aggregates[1].NoteCount);
        Assert.Equal(1, aggregates[1].MissCount);
        Assert.Equal(0, aggregates[1].SumEarnedScore);

        Assert.Equal(2, await service.RebuildUserAsync(userId));
        Assert.Equal(3, await db.PlayNoteAggregates.Where(a => a.UserId == userId).SumAsync(a => a.NoteCount));
    }

    private async Task<(Guid UserId, Guid First, Guid Second)> SeedAsync() {
        await using var db = new BeatDashDbContext(_options);
        var user = new User { DisplayName = "Weakness" };
        var map = new Beatmap { LevelId = "weakness", SongName = "W", SongAuthor = "T", Mapper = "T", Bpm = 120, DurationMs = 1 };
        var difficulty = new BeatmapDifficulty {
            DifficultyRank = BeatmapDifficultyRank.Expert, DifficultyName = "Expert", NotesPerSecond = 1, CuttableObjectCount = 3,
            BombCount = 0, ObstacleCount = 0, LaneCount = 4, CharacteristicSerializedName = "Standard", CharacteristicColorCount = 2,
            CharacteristicRequires360Movement = false, CharacteristicContainsRotationEvents = false
        };
        map.Difficulties.Add(difficulty);
        db.AddRange(user, map);
        var first = Session(user, difficulty, 1, Note(0, 0), Note(1, 2));
        var second = Session(user, difficulty, 2, Note(0, 0));
        db.PlaySessions.AddRange(first, second);
        await db.SaveChangesAsync();
        return (user.Id, first.Id, second.Id);
    }

    private static PlaySession Session(User user, BeatmapDifficulty difficulty, int correlationId, params PlaySessionNoteItem[] notes) {
        var session = new PlaySession {
            CorrelationId = correlationId, User = user, BeatmapDifficulty = difficulty, StartedAt = DateTime.UtcNow,
            EndedAt = DateTime.UtcNow, EndReason = PlaySessionEndReason.Finished,
            Results = new PlaySessionResults {
                Score = 1, MultipliedScore = 1, MaxPossibleScore = 1, Accuracy = 1, Rank = "S", FullCombo = false, MaxCombo = 1,
                GoodCuts = 1, BadCuts = 0, Misses = 0, FinalEnergy = 1, EndSongTimeMs = 1
            }
        };
        foreach (var note in notes) session.NoteItems.Add(note);
        return session;
    }

    private static PlaySessionNoteItem Note(int lineIndex, int result) => new() {
        CorrelationId = 0, PlaySessionId = Guid.Empty, SongTimeMs = lineIndex, ColorType = ColorType.A, NoteType = NoteType.Normal,
        ScoringType = NoteScoringType.Normal, CutDirection = CutDirection.Down, LineIndex = lineIndex, NoteLineLayer = 0,
        Result = result, MaxScore = 115, BeforeCutScore = 70, CenterDistanceScore = 15, AfterCutScore = 15,
        PreCutSwing = 1, PostCutSwing = 1, CutPointDistance = 0, SaberSpeed = 1
    };

    public Task DisposeAsync() {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        return Task.CompletedTask;
    }

    private sealed class Factory(DbContextOptions<BeatDashDbContext> options) : IDbContextFactory<BeatDashDbContext> {
        public BeatDashDbContext CreateDbContext() => new(options);
    }
}
