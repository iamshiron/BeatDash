using System.IO.Compression;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
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
using Shiron.BeatDash.Data.Backup;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;
using Xunit;

namespace Shiron.BeatDash.API.Tests;

public sealed class BackupIntegrationTests {
    [BackupIntegrationFact]
    public async Task ApiAndCli_BackupRoundTrip_EnforceAdminAuthenticationAndRevocation() {
        await using var fixture = await Fixture.CreateAsync();
        var ordinary = new User {
            DisplayName = "Ordinary", UserName = "ordinary", NormalizedUserName = "ORDINARY",
            Email = "ordinary@example.test", NormalizedEmail = "ORDINARY@EXAMPLE.TEST", SecurityStamp = Guid.NewGuid().ToString()
        };
        ordinary.PasswordHash = new PasswordHasher<User>().HashPassword(ordinary, Fixture.Password);
        fixture.Db.Users.Add(ordinary);
        await fixture.Db.SaveChangesAsync();
        using var http = await fixture.StartApiAsync();

        using (var anonymous = await http.PostAsJsonAsync("/api/admin/backups/export", new BackupExportRequest(BackupScope.Both)))
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using (var login = await http.PostAsJsonAsync("/api/admin/backups/login", new BackupLoginRequest(ordinary.Email!, Fixture.Password)))
            Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        using (var login = await http.PostAsJsonAsync("/api/admin/backups/login", new BackupLoginRequest(Fixture.Email, "wrong")))
            Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        var tokenService = new TokenService(Fixture.JwtSecret, "backup-test", "backup-test");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenService.GenerateAccessToken(ordinary.Id, out _));
        using (var forbidden = await http.PostAsJsonAsync("/api/admin/backups/export", new BackupExportRequest(BackupScope.Both)))
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        await fixture.RunCliAsync(http.BaseAddress!, "export");
        await fixture.RunCliAsync(http.BaseAddress!, "restore");

        using var adminLogin = await http.PostAsJsonAsync("/api/admin/backups/login", new BackupLoginRequest(Fixture.Email, Fixture.Password));
        adminLogin.EnsureSuccessStatusCode();
        var session = await adminLogin.Content.ReadFromJsonAsync<BackupLoginResponse>();
        Assert.NotNull(session);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var admin = await fixture.Db.Users.SingleAsync(u => u.NormalizedEmail == Fixture.Email.ToUpperInvariant());
        await fixture.Db.UserRoles.Where(r => r.UserId == admin.Id).ExecuteDeleteAsync();
        using var revoked = await http.PostAsJsonAsync("/api/admin/backups/export", new BackupExportRequest(BackupScope.Both));
        Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
    }

    [BackupIntegrationFact]
    public async Task Restore_Both_ExactlyRoundTripsEveryTableSequenceAndStorageObject() {
        await using var fixture = await Fixture.CreateAsync();
        var database = new BackupDatabase(fixture.Db);
        foreach (var table in database.Tables) {
            var columns = string.Join(", ", table.Columns.Select(c => BackupDatabase.Quote(c.Name)));
            var values = string.Join(", ", table.Columns.Select(c => Value(c.StoreType)));
            await ExecuteAsync(fixture.Db, $"INSERT INTO {BackupDatabase.TableName(table)} ({columns}) VALUES ({values})");
        }
        foreach (var sequence in await database.GetSequencesAsync(default))
            await ExecuteAsync(fixture.Db, $"SELECT setval('{sequence.Name}', 40, false)");
        await fixture.Storage.UploadAsync(fixture.StorageOptions.BucketAssets, "maps/test.zip", "application/zip", [0, 255, 42], default);
        await fixture.Storage.UploadAsync(fixture.StorageOptions.BucketUserData, "avatars/test.png", "image/png", [1, 2, 3], default);
        var original = await fixture.Service.ExportAsync(BackupScope.Both, default);
        await ExecuteAsync(fixture.Db, "TRUNCATE " + string.Join(", ", database.Tables.Select(BackupDatabase.TableName)));
        await fixture.Storage.UploadAsync(fixture.StorageOptions.BucketAssets, "maps/test.zip", "text/plain", [9], default);
        await fixture.Storage.UploadAsync(fixture.StorageOptions.BucketUserData, "extra", "text/plain", [8], default);

        await using (var input = File.OpenRead(original))
            await fixture.Service.RestoreAsync(input, BackupScope.Both, default);

        var restored = await fixture.Service.ExportAsync(BackupScope.Both, default);
        using var beforeArchive = ZipFile.OpenRead(original);
        using var afterArchive = ZipFile.OpenRead(restored);
        var before = await BackupArchive.ValidateAsync(beforeArchive, 10_000_000, default);
        var after = await BackupArchive.ValidateAsync(afterArchive, 10_000_000, default);
        Assert.Equal(before.Tables, after.Tables);
        Assert.Equal(before.Objects, after.Objects);
        Assert.Equal(before.Sequences, after.Sequences);
        Assert.Equal(database.Tables.Length, after.Tables.Length);
        Assert.Equal(2, after.Objects.Length);
        foreach (var sequence in after.Sequences) {
            await using var command = new NpgsqlCommand($"SELECT nextval('{sequence.Name}')", (NpgsqlConnection) fixture.Db.Database.GetDbConnection());
            Assert.Equal(40L, await command.ExecuteScalarAsync());
        }
    }

    [BackupIntegrationFact]
    public async Task Restore_DatabaseScope_DoesNotModifyStorage() {
        await using var fixture = await Fixture.CreateAsync();
        var original = await fixture.Service.ExportAsync(BackupScope.Both, default);
        await fixture.Storage.UploadAsync(fixture.StorageOptions.BucketAssets, "later", "text/plain", [7], default);
        await using var input = File.OpenRead(original);
        await fixture.Service.RestoreAsync(input, BackupScope.Database, default);
        Assert.Equal(new byte[] { 7 }, await fixture.Storage.DownloadAsync(fixture.StorageOptions.BucketAssets, "later", default));
    }

    [BackupIntegrationFact]
    public async Task Restore_InvalidDatabasePayload_RollsBackDatabaseAndRetainsRecoverableStorageSnapshot() {
        await using var fixture = await Fixture.CreateAsync();
        var user = new User { DisplayName = "Before" };
        fixture.Db.Users.Add(user);
        await fixture.Db.SaveChangesAsync();
        await fixture.Storage.UploadAsync(fixture.StorageOptions.BucketAssets, "object", "text/plain", [1], default);
        var original = await fixture.Service.ExportAsync(BackupScope.Both, default);
        BackupManifest manifest;
        using (var zip = ZipFile.OpenRead(original))
            manifest = await BackupArchive.ValidateAsync(zip, 10_000_000, default);
        using (var zip = ZipFile.Open(original, ZipArchiveMode.Update)) {
            var table = manifest.Tables.Last();
            var invalid = new byte[] { 1, 2, 3 };
            zip.GetEntry(table.Payload.Entry)!.Delete();
            await using (var output = zip.CreateEntry(table.Payload.Entry).Open())
                await output.WriteAsync(invalid);
            var changed = table with {
                Payload = table.Payload with {
                    Length = invalid.Length,
                    Sha256 = Convert.ToHexString(SHA256.HashData(invalid))
                }
            };
            manifest = manifest with { Tables = manifest.Tables.Select(t => t == table ? changed : t).ToArray() };
            zip.GetEntry("manifest.json")!.Delete();
            await using var metadata = zip.CreateEntry("manifest.json").Open();
            await JsonSerializer.SerializeAsync(metadata, manifest);
        }
        await fixture.Storage.UploadAsync(fixture.StorageOptions.BucketAssets, "object", "text/plain", [2], default);
        await using (var input = File.OpenRead(original))
            await Assert.ThrowsAnyAsync<Exception>(() => fixture.Service.RestoreAsync(input, BackupScope.Both, default));

        fixture.Db.ChangeTracker.Clear();
        Assert.Equal("Before", (await fixture.Db.Users.SingleAsync()).DisplayName);
        Assert.Equal(new byte[] { 1 }, await fixture.Storage.DownloadAsync(fixture.StorageOptions.BucketAssets, "object", default));
        await fixture.Service.RecoverAsync(default);
        Assert.Equal(new byte[] { 2 }, await fixture.Storage.DownloadAsync(fixture.StorageOptions.BucketAssets, "object", default));
        Assert.Equal("Before", (await fixture.Db.Users.AsNoTracking().SingleAsync()).DisplayName);
    }

    private static string Value(string storeType) => storeType switch {
        "uuid" => "'12345678-1234-1234-1234-123456789012'",
        "integer" or "bigint" or "smallint" => "7",
        "boolean" => "false",
        "real" or "double precision" or "numeric" => "1.25",
        "timestamp with time zone" or "timestamp without time zone" => "'2026-01-02 03:04:05+00'",
        "jsonb" or "json" => "'{\"sample\":1}'",
        "text[]" => "ARRAY['one', 'two']",
        "bytea" => "'\\x00ff'",
        "text" => "'sample'",
        _ when storeType.StartsWith("character varying", StringComparison.Ordinal) => "'sample'",
        _ => throw new InvalidOperationException($"Add integration fixture data for {storeType}.")
    };

    private static async Task ExecuteAsync(BeatDashDbContext db, string sql) {
        await db.Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, (NpgsqlConnection) db.Database.GetDbConnection());
        await command.ExecuteNonQueryAsync();
    }

    private sealed class BackupIntegrationFactAttribute : FactAttribute {
        public BackupIntegrationFactAttribute() {
            if (Environment.GetEnvironmentVariable("BEATDASH_TEST_POSTGRES") is null
                || Environment.GetEnvironmentVariable("BEATDASH_TEST_MINIO") is null)
                Skip = "Set BEATDASH_TEST_POSTGRES and BEATDASH_TEST_MINIO to disposable test infrastructure.";
        }
    }

    private sealed class Fixture : IAsyncDisposable {
        public const string Email = "admin@example.test";
        public const string Password = "Backup-test-Password1!";
        public const string JwtSecret = "backup-integration-test-secret-at-least-32-characters";
        private readonly string _adminConnection = Environment.GetEnvironmentVariable("BEATDASH_TEST_POSTGRES")!;
        private readonly string _databaseName = "backup_test_" + Guid.NewGuid().ToString("N");
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "beatdash-backup-tests", Guid.NewGuid().ToString("N"));
        private readonly IMinioClient _minio;
        private readonly BackupWorkspace _workspace;
        private Process? _server;
        public BeatDashDbContext Db { get; }
        public StorageOptions StorageOptions { get; }
        public IStorageService Storage { get; }
        public BackupService Service { get; }

        private Fixture() {
            var connection = new NpgsqlConnectionStringBuilder(_adminConnection) { Database = _databaseName };
            Db = new BeatDashDbContext(new DbContextOptionsBuilder<BeatDashDbContext>().UseNpgsql(connection.ConnectionString).Options);
            StorageOptions = new StorageOptions {
                BucketAssets = "assets-" + Guid.NewGuid().ToString("N"), BucketUserData = "users-" + Guid.NewGuid().ToString("N")
            };
            _minio = new MinioClient().WithEndpoint(Environment.GetEnvironmentVariable("BEATDASH_TEST_MINIO"))
                .WithCredentials("backup-test", "backup-test-secret").Build();
            Storage = new MinioStorageService(_minio, NullLogger<MinioStorageService>.Instance);
            var backupOptions = Options.Create(new BackupOptions { MaintenanceMode = true, WorkDirectory = _directory });
            _workspace = new BackupWorkspace(backupOptions);
            Service = new BackupService(new BackupDatabase(Db), new BackupStorage(_minio, Storage, Options.Create(StorageOptions)),
                _workspace, backupOptions, NullLogger<BackupService>.Instance);
        }

        public static async Task<Fixture> CreateAsync() {
            var fixture = new Fixture();
            await using var admin = new NpgsqlConnection(fixture._adminConnection);
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE {BackupDatabase.Quote(fixture._databaseName)}", admin);
            await command.ExecuteNonQueryAsync();
            await fixture.Db.Database.EnsureCreatedAsync();
            return fixture;
        }

        public async Task<HttpClient> StartApiAsync() {
            Directory.CreateDirectory(_directory);
            var start = new ProcessStartInfo("dotnet") {
                WorkingDirectory = _directory, RedirectStandardOutput = true, RedirectStandardError = true
            };
            start.ArgumentList.Add(typeof(BackupService).Assembly.Location);
            start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
            start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
            start.Environment["ConnectionStrings__Default"] = Db.Database.GetConnectionString();
            start.Environment["Jwt__SecretKey"] = JwtSecret;
            start.Environment["Jwt__Issuer"] = "backup-test";
            start.Environment["Jwt__Audience"] = "backup-test";
            start.Environment["AdminUser__Email"] = Email;
            start.Environment["AdminUser__Password"] = Password;
            start.Environment["AdminUser__Role"] = "Admin";
            start.Environment["Backup__MaintenanceMode"] = "true";
            start.Environment["Backup__WorkDirectory"] = Path.Combine(_directory, "server-work");
            start.Environment["MINIO_ENDPOINT"] = Environment.GetEnvironmentVariable("BEATDASH_TEST_MINIO");
            start.Environment["MINIO_ACCESS_KEY"] = "backup-test";
            start.Environment["MINIO_SECRET_KEY"] = "backup-test-secret";
            start.Environment["MINIO_BUCKET_ASSETS"] = StorageOptions.BucketAssets;
            start.Environment["MINIO_BUCKET_USER_DATA"] = StorageOptions.BucketUserData;
            var ready = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
            _server = new Process { StartInfo = start, EnableRaisingEvents = true };
            var errors = new System.Text.StringBuilder();
            _server.OutputDataReceived += (_, args) => {
                const string prefix = "Now listening on: ";
                if (args.Data is { } line && line.IndexOf(prefix, StringComparison.Ordinal) is var index && index >= 0)
                    ready.TrySetResult(new Uri(line[(index + prefix.Length)..].Trim()));
            };
            _server.ErrorDataReceived += (_, args) => { lock (errors) errors.AppendLine(args.Data); };
            _server.Exited += (_, _) => { lock (errors) ready.TrySetException(new InvalidOperationException(errors.ToString())); };
            _server.Start();
            _server.BeginOutputReadLine();
            _server.BeginErrorReadLine();
            var address = await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
            return new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = address };
        }

        public async Task RunCliAsync(Uri address, string command) {
            var start = new ProcessStartInfo("dotnet") {
                WorkingDirectory = _directory, RedirectStandardOutput = true, RedirectStandardError = true
            };
            start.ArgumentList.Add(typeof(Shiron.BeatDash.Cli.Commands.BackupExportCommand).Assembly.Location);
            foreach (var arg in new[] { "backup", command, Path.Combine(_directory, "cli-backup.zip"), "--host", address.ToString(), "--scope", "Both" })
                start.ArgumentList.Add(arg);
            if (command == "restore") start.ArgumentList.Add("--yes");
            start.Environment["BEATDASH_ADMIN_EMAIL"] = Email;
            start.Environment["BEATDASH_ADMIN_PASSWORD"] = Password;
            using var cli = Process.Start(start)!;
            var output = cli.StandardOutput.ReadToEndAsync();
            var errors = cli.StandardError.ReadToEndAsync();
            await cli.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(cli.ExitCode == 0, await output + await errors);
        }

        public async ValueTask DisposeAsync() {
            if (_server is not null) {
                if (!_server.HasExited) _server.Kill(entireProcessTree: true);
                await _server.WaitForExitAsync();
                _server.Dispose();
            }
            await Db.DisposeAsync();
            await using var admin = new NpgsqlConnection(_adminConnection);
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE {BackupDatabase.Quote(_databaseName)} WITH (FORCE)", admin);
            await command.ExecuteNonQueryAsync();
            foreach (var bucket in new[] { StorageOptions.BucketAssets, StorageOptions.BucketUserData }) {
                if (!await _minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket))) continue;
                await foreach (var item in _minio.ListObjectsEnumAsync(new ListObjectsArgs().WithBucket(bucket).WithRecursive(true)))
                    await _minio.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(bucket).WithObject(item.Key));
                await _minio.RemoveBucketAsync(new RemoveBucketArgs().WithBucket(bucket));
            }
            _minio.Dispose();
            _workspace.Dispose();
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }
    }
}
