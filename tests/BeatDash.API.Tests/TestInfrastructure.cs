using System.Data.Common;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Npgsql;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.API.Services;
using Shiron.BeatDash.API.Services.Backup;
using Shiron.BeatDash.API.Services.Impl;
using Shiron.BeatDash.DB;

namespace Shiron.BeatDash.API.Tests;

/// <summary>A disposable database and object storage for one integration test, on either <see cref="TestBackend"/>.</summary>
public sealed class TestInfrastructure : IAsyncDisposable {
    private const string RustFsAccessKey = "backup-test";
    private const string RustFsSecretKey = "backup-test-secret";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "beatdash-test-infrastructure", Guid.NewGuid().ToString("N"));
    private readonly string _databaseName = "beatdash_test_" + Guid.NewGuid().ToString("N");
    private readonly IMinioClient? _minio;

    private TestInfrastructure(TestBackend backend) {
        Backend = backend;
        StorageOptions = new StorageOptions {
            BucketAssets = "assets-" + Guid.NewGuid().ToString("N"),
            BucketUserData = "users-" + Guid.NewGuid().ToString("N"),
            RootDirectory = Path.Combine(_directory, "storage")
        };
        switch (backend) {
            case TestBackend.PostgresRustFs:
                DatabaseProvider = DatabaseProvider.Postgres;
                ConnectionString = new NpgsqlConnectionStringBuilder(PostgresAdminConnection) { Database = _databaseName }.ConnectionString;
                StorageOptions.Provider = StorageProvider.RustFs;
                _minio = new MinioClient().WithEndpoint(Environment.GetEnvironmentVariable("BEATDASH_TEST_RUSTFS"))
                    .WithCredentials(RustFsAccessKey, RustFsSecretKey).Build();
                Storage = new MinioStorageService(_minio, NullLogger<MinioStorageService>.Instance);
                BackupStorage = new MinioBackupStorage(_minio, Storage, Options.Create(StorageOptions));
                break;
            case TestBackend.SqliteFileSystem:
                DatabaseProvider = DatabaseProvider.Sqlite;
                ConnectionString = $"Data Source={Path.Combine(_directory, "beatdash.db")}";
                StorageOptions.Provider = StorageProvider.FileSystem;
                var storage = new FileSystemStorageService(Options.Create(StorageOptions));
                Storage = storage;
                BackupStorage = new FileSystemBackupStorage(storage, Options.Create(StorageOptions));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(backend));
        }
    }

    private static string PostgresAdminConnection => Environment.GetEnvironmentVariable("BEATDASH_TEST_POSTGRES")!;

    public TestBackend Backend { get; }
    public DatabaseProvider DatabaseProvider { get; }
    public string ConnectionString { get; }
    public StorageOptions StorageOptions { get; }
    public IStorageService Storage { get; }
    public IBackupStorage BackupStorage { get; }

    /// <summary>Provisions an empty database; the caller creates the schema.</summary>
    public static async Task<TestInfrastructure> CreateAsync(TestBackend backend) {
        var infrastructure = new TestInfrastructure(backend);
        Directory.CreateDirectory(infrastructure._directory);
        if (backend == TestBackend.PostgresRustFs) {
            await using var admin = new NpgsqlConnection(PostgresAdminConnection);
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE {BackupDatabase.Quote(infrastructure._databaseName)}", admin);
            await command.ExecuteNonQueryAsync();
        }
        return infrastructure;
    }

    public BeatDashDbContext CreateDbContext() {
        var options = new DbContextOptionsBuilder<BeatDashDbContext>();
        options.UseBeatDashDatabase(DatabaseProvider, ConnectionString);
        return new BeatDashDbContext(options.Options);
    }

    public BackupDatabase CreateBackupDatabase(BeatDashDbContext db) => DatabaseProvider switch {
        DatabaseProvider.Postgres => new PostgresBackupDatabase(db),
        _ => new SqliteBackupDatabase(db)
    };

    /// <summary>Points a spawned API process at this database and storage.</summary>
    public void ConfigureServer(ProcessStartInfo start) {
        start.Environment["Database__Provider"] = DatabaseProvider.ToString();
        start.Environment["ConnectionStrings__Default"] = ConnectionString;
        start.Environment["Storage__Provider"] = StorageOptions.Provider.ToString();
        start.Environment["Storage__RootDirectory"] = StorageOptions.RootDirectory;
        start.Environment["Storage__BucketAssets"] = StorageOptions.BucketAssets;
        start.Environment["Storage__BucketUserData"] = StorageOptions.BucketUserData;
        if (Backend != TestBackend.PostgresRustFs) return;
        start.Environment["RUSTFS_ENDPOINT"] = Environment.GetEnvironmentVariable("BEATDASH_TEST_RUSTFS");
        start.Environment["RUSTFS_ACCESS_KEY"] = RustFsAccessKey;
        start.Environment["RUSTFS_SECRET_KEY"] = RustFsSecretKey;
    }

    /// <summary>Runs raw SQL on the context's connection.</summary>
    public static async Task ExecuteSqlAsync(BeatDashDbContext db, string sql) {
        await db.Database.OpenConnectionAsync();
        await using DbCommand command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync() {
        if (Backend == TestBackend.PostgresRustFs) {
            await using (var admin = new NpgsqlConnection(PostgresAdminConnection)) {
                await admin.OpenAsync();
                await using var command = new NpgsqlCommand($"DROP DATABASE {BackupDatabase.Quote(_databaseName)} WITH (FORCE)", admin);
                await command.ExecuteNonQueryAsync();
            }
            var minio = _minio!;
            foreach (var bucket in new[] { StorageOptions.BucketAssets, StorageOptions.BucketUserData }) {
                if (!await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket))) continue;
                await foreach (var item in minio.ListObjectsEnumAsync(new ListObjectsArgs().WithBucket(bucket).WithRecursive(true)))
                    await minio.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(bucket).WithObject(item.Key));
                await minio.RemoveBucketAsync(new RemoveBucketArgs().WithBucket(bucket));
            }
            minio.Dispose();
        }
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
