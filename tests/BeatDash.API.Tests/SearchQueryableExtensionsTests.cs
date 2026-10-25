using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;
using Xunit;

namespace Shiron.BeatDash.API.Tests;

public sealed class SearchQueryableExtensionsTests {
    [Fact]
    public void WhereAnyLikeIgnoreCase_Postgres_TranslatesToParameterizedILike() {
        var options = new DbContextOptionsBuilder<BeatDashDbContext>();
        options.UseBeatDashDatabase(DatabaseProvider.Postgres, "Host=unused;Database=unused");
        using var db = new BeatDashDbContext(options.Options);

        var sql = db.Beatmaps.WhereAnyLikeIgnoreCase(db.Database, "%Tech%", b => b.SongName, b => b.SongSubName).ToQueryString();

        Assert.Contains("b.\"SongName\" ILIKE @pattern", sql, StringComparison.Ordinal);
        Assert.Contains("b.\"SongSubName\" ILIKE @pattern", sql, StringComparison.Ordinal);
        Assert.StartsWith("-- @pattern='%Tech%'", sql, StringComparison.Ordinal);
        Assert.Equal(1, sql.Split("'%Tech%'").Length - 1);
    }

    [Fact]
    public async Task WhereAnyLikeIgnoreCase_Sqlite_MatchesAnyColumnIgnoringCase() {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<BeatDashDbContext>();
        options.UseSqlite(connection);
        await using var db = new BeatDashDbContext(options.Options);
        await db.Database.EnsureCreatedAsync();
        db.Beatmaps.AddRange(
            new Beatmap { LevelId = "a", SongName = "Technical Song", SongAuthor = "x", Mapper = "x", Bpm = 120, DurationMs = 1 },
            new Beatmap { LevelId = "b", SongName = "Other", SongSubName = "TECHNO remix", SongAuthor = "x", Mapper = "x", Bpm = 120, DurationMs = 1 },
            new Beatmap { LevelId = "c", SongName = "Unrelated", SongAuthor = "x", Mapper = "x", Bpm = 120, DurationMs = 1 });
        await db.SaveChangesAsync();

        var levels = await db.Beatmaps
            .WhereAnyLikeIgnoreCase(db.Database, "%tech%", b => b.SongName, b => b.SongSubName)
            .Select(b => b.LevelId).OrderBy(id => id).ToListAsync();

        Assert.Equal(["a", "b"], levels);
        Assert.Contains("LIKE @pattern", db.Beatmaps.WhereAnyLikeIgnoreCase(db.Database, "%x%", b => b.SongName).ToQueryString(),
            StringComparison.Ordinal);
    }
}
