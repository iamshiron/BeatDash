using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.API.Services.Backup;
using Shiron.BeatDash.API.Services.Impl;
using Shiron.BeatDash.Data.Backup;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;
using Xunit;

namespace Shiron.BeatDash.API.Tests;

public sealed class SqliteBackupDatabaseTests : IAsyncLifetime {
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "beatdash-sqlite-backup-tests", Guid.NewGuid().ToString("N"));
    private BeatDashDbContext _db = null!;
    private BackupWorkspace _workspace = null!;
    private SqliteBackupDatabase _database = null!;
    private BackupService _service = null!;

    public async Task InitializeAsync() {
        Directory.CreateDirectory(_directory);
        var options = new DbContextOptionsBuilder<BeatDashDbContext>();
        options.UseBeatDashDatabase(DatabaseProvider.Sqlite, $"Data Source={Path.Combine(_directory, "beatdash.db")};Pooling=False");
        _db = new BeatDashDbContext((DbContextOptions<BeatDashDbContext>) options.Options);
        await _db.Database.InitializeAsync(applyMigrations: true);
        var backupOptions = Options.Create(new BackupOptions {
            MaintenanceMode = true, WorkDirectory = Path.Combine(_directory, "work"), MaxArchiveBytes = 10_000_000
        });
        _workspace = new BackupWorkspace(backupOptions);
        var storageOptions = Options.Create(new StorageOptions { RootDirectory = Path.Combine(_directory, "storage") });
        _database = new SqliteBackupDatabase(_db);
        _service = new BackupService(_database, new FileSystemBackupStorage(new FileSystemStorageService(storageOptions), storageOptions),
            _workspace, backupOptions, NullLogger<BackupService>.Instance);
    }

    [Fact]
    public void Tables_ApplicationModel_UsesUnqualifiedNamesInDependencyOrder() {
        var tables = _database.Tables;

        Assert.Equal(_db.Model.GetRelationalModel().Tables.Count(), tables.Length);
        Assert.Equal("\"Users\"", _database.TableName(tables.Single(t => t.Name == "Users")));
        foreach (var table in tables) {
            foreach (var foreignKey in table.ForeignKeyConstraints.Where(f => f.PrincipalTable != table))
                Assert.True(Array.IndexOf(tables, foreignKey.PrincipalTable) < Array.IndexOf(tables, table));
        }
    }

    [Fact]
    public async Task Restore_Database_ExactlyRoundTripsEveryTableAndSequence() {
        await SeedEveryTableAsync();
        await ExecuteAsync("UPDATE sqlite_sequence SET seq = 40");
        // Text in an INTEGER column keeps its storage class; restore must not coerce it.
        await ExecuteAsync("UPDATE \"Beatmaps\" SET \"DurationMs\" = 'not a number', \"SongSubName\" = NULL");
        var original = await _service.ExportAsync(BackupScope.Database, default);
        foreach (var table in Enumerable.Reverse(_database.Tables)) await ExecuteAsync($"DELETE FROM {_database.TableName(table)}");
        await ExecuteAsync("DELETE FROM sqlite_sequence");

        await using (var input = File.OpenRead(original))
            await _service.RestoreAsync(input, BackupScope.Database, default);

        var restored = await _service.ExportAsync(BackupScope.Database, default);
        var before = await ReadManifestAsync(original);
        var after = await ReadManifestAsync(restored);
        Assert.Equal(BackupDatabaseEngine.Sqlite, after.DatabaseEngine);
        Assert.Equal(0, after.PostgresMajorVersion);
        Assert.Equal(before.Tables, after.Tables);
        Assert.Equal(before.Sequences, after.Sequences);
        Assert.Equal(_database.Tables.Length, after.Tables.Length);
        Assert.NotEmpty(after.Sequences);
        Assert.All(after.Sequences, s => Assert.Equal((40L, true), (s.LastValue, s.IsCalled)));
        Assert.All(after.Tables, t => Assert.True(t.Payload.Length > 1, t.Name));
        Assert.Equal("text|null", await ScalarAsync("SELECT typeof(\"DurationMs\") || '|' || typeof(\"SongSubName\") FROM \"Beatmaps\""));
    }

    [Fact]
    public async Task Restore_InvalidTablePayload_RollsBackTheWholeDatabase() {
        _db.Users.Add(new User { DisplayName = "Before" });
        await _db.SaveChangesAsync();
        var archive = await _service.ExportAsync(BackupScope.Database, default);
        await RewriteAsync(archive, manifest => {
            var table = manifest.Tables.Last();
            return (manifest, table.Payload.Entry, [1, 7, 2]);
        });
        await _db.Users.ExecuteUpdateAsync(s => s.SetProperty(u => u.DisplayName, "Changed"));

        await using (var input = File.OpenRead(archive))
            await Assert.ThrowsAnyAsync<Exception>(() => _service.RestoreAsync(input, BackupScope.Database, default));

        Assert.Equal("Changed", (await _db.Users.AsNoTracking().SingleAsync()).DisplayName);
        await _service.RecoverAsync(default);
        Assert.Equal("Changed", (await _db.Users.AsNoTracking().SingleAsync()).DisplayName);
    }

    [Fact]
    public async Task Restore_PostgresArchive_IsRejectedBeforeAnyChange() {
        _db.Users.Add(new User { DisplayName = "Before" });
        await _db.SaveChangesAsync();
        var archive = await _service.ExportAsync(BackupScope.Database, default);
        await RewriteAsync(archive, manifest => (manifest with { DatabaseEngine = BackupDatabaseEngine.Postgres }, null, null));

        await using var input = File.OpenRead(archive);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => _service.RestoreAsync(input, BackupScope.Database, default));

        Assert.Contains("Postgres", error.Message, StringComparison.Ordinal);
        Assert.Equal("Before", (await _db.Users.AsNoTracking().SingleAsync()).DisplayName);
        Assert.False(File.Exists(Path.Combine(_directory, "work", "recovery.zip")));
    }

    private async Task SeedEveryTableAsync() {
        foreach (var table in _database.Tables) {
            var columns = string.Join(", ", table.Columns.Select(c => BackupDatabase.Quote(c.Name)));
            var values = string.Join(", ", table.Columns.Select(c => c.StoreType switch {
                "TEXT" => "'sample'",
                "INTEGER" => "7",
                "REAL" => "1.25",
                "BLOB" => "x'00ff'",
                _ => throw new InvalidOperationException($"Add fixture data for {c.StoreType}.")
            }));
            await ExecuteAsync($"INSERT INTO {_database.TableName(table)} ({columns}) VALUES ({values})");
        }
    }

    private async Task ExecuteAsync(string sql) {
        await _db.Database.OpenConnectionAsync();
        await using var command = new SqliteCommand(sql, (SqliteConnection) _db.Database.GetDbConnection());
        await command.ExecuteNonQueryAsync();
    }

    private async Task<object?> ScalarAsync(string sql) {
        await _db.Database.OpenConnectionAsync();
        await using var command = new SqliteCommand(sql, (SqliteConnection) _db.Database.GetDbConnection());
        return await command.ExecuteScalarAsync();
    }

    private static async Task<BackupManifest> ReadManifestAsync(string path) {
        using var archive = ZipFile.OpenRead(path);
        return await BackupArchive.ValidateAsync(archive, 10_000_000, default);
    }

    private static async Task RewriteAsync(string path, Func<BackupManifest, (BackupManifest Manifest, string? Entry, byte[]? Content)> change) {
        var (manifest, entry, content) = change(await ReadManifestAsync(path));
        using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
        if (entry is not null && content is not null) {
            zip.GetEntry(entry)!.Delete();
            await using (var output = zip.CreateEntry(entry).Open()) await output.WriteAsync(content);
            manifest = manifest with {
                Tables = manifest.Tables.Select(t => t.Payload.Entry != entry ? t
                    : t with { Payload = t.Payload with { Length = content.Length, Sha256 = Convert.ToHexString(SHA256.HashData(content)) } })
                    .ToArray()
            };
        }
        zip.GetEntry("manifest.json")!.Delete();
        await using var metadata = zip.CreateEntry("manifest.json").Open();
        await JsonSerializer.SerializeAsync(metadata, manifest);
    }

    public async Task DisposeAsync() {
        _workspace.Dispose();
        await _db.DisposeAsync();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
