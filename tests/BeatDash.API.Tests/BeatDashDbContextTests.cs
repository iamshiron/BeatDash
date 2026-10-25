using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;
using Xunit;

namespace Shiron.BeatDash.API.Tests;

/// <summary>Guards database model compatibility when updating EF Core and the database providers.</summary>
public sealed class BeatDashDbContextTests {
    [Fact]
    public void HasPendingModelChanges_CurrentModel_MatchesMigrationSnapshot() {
        using var context = Create(DatabaseProvider.Postgres, "Host=localhost;Database=beatdash_model_validation;Username=unused");

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void HasPendingModelChanges_SqliteModel_MatchesSqliteMigrationSnapshot() {
        using var context = Create(DatabaseProvider.Sqlite, "Data Source=:memory:");

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Model_Postgres_StoresJsonDocumentsAsJsonb() {
        using var context = Create(DatabaseProvider.Postgres, "Host=unused;Database=unused");

        var column = context.Model.FindEntityType(typeof(PlaySessionMotionSummary))!.FindProperty(nameof(PlaySessionMotionSummary.FatigueCurve))!;
        Assert.Equal("jsonb", column.GetColumnType());
    }

    [Fact]
    public async Task Migrate_Sqlite_TimestampsReadBackAsUtc() {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<BeatDashDbContext>();
        options.UseSqlite(connection, o => o.MigrationsAssembly(BeatDashDatabaseExtensions.SqliteMigrationsAssembly));
        var createdAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        await using (var db = new BeatDashDbContext(options.Options)) {
            await db.Database.MigrateAsync();
            db.Beatmaps.Add(new Beatmap {
                LevelId = "utc", SongName = "Utc", SongAuthor = "Tests", Mapper = "Tests", Bpm = 120, DurationMs = 1,
                CreatedAt = createdAt, FetchLastAttemptedAt = createdAt
            });
            await db.SaveChangesAsync();
        }

        await using var read = new BeatDashDbContext(options.Options);
        var map = await read.Beatmaps.SingleAsync(b => b.CreatedAt >= createdAt.AddSeconds(-1));
        Assert.Equal(DateTimeKind.Utc, map.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, map.FetchLastAttemptedAt!.Value.Kind);
        Assert.Equal(createdAt, map.CreatedAt);
    }

    private static BeatDashDbContext Create(DatabaseProvider provider, string connectionString) {
        var options = new DbContextOptionsBuilder<BeatDashDbContext>();
        options.UseBeatDashDatabase(provider, connectionString);
        return new BeatDashDbContext(options.Options);
    }
}
