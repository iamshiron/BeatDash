using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.API.Services.Backup;
using Shiron.BeatDash.API.Services.Impl;
using Shiron.BeatDash.Data.Backup;
using Shiron.BeatDash.DB;
using Xunit;

namespace Shiron.BeatDash.API.Tests;

public sealed class FileSystemBackupStorageTests : IDisposable {
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "beatdash-fs-backup-tests", Guid.NewGuid().ToString("N"));
    private readonly StorageOptions _storageOptions;
    private readonly FileSystemStorageService _storage;
    private readonly FileSystemBackupStorage _backup;

    public FileSystemBackupStorageTests() {
        _storageOptions = new StorageOptions { RootDirectory = Path.Combine(_directory, "storage") };
        _storage = new FileSystemStorageService(Options.Create(_storageOptions));
        _backup = new FileSystemBackupStorage(_storage, Options.Create(_storageOptions));
    }

    [Fact]
    public async Task ListAsync_BothBuckets_MapsToLogicalBucketsWithContentTypeAndLength() {
        await _storage.UploadAsync(_storageOptions.BucketAssets, "maps/a.zip", "application/zip", [0, 255, 42], default);
        await _storage.UploadAsync(_storageOptions.BucketUserData, "avatars/a.png", "image/png", [1, 2], default);

        var items = new List<BackupObjectDTO>();
        await foreach (var item in _backup.ListAsync(default)) items.Add(item);

        Assert.Equal([
            new BackupObjectDTO("assets", "maps/a.zip", "application/zip", new BackupPayloadDTO("", 3, "")),
            new BackupObjectDTO("user-data", "avatars/a.png", "image/png", new BackupPayloadDTO("", 2, ""))
        ], items);
    }

    [Fact]
    public async Task Restore_StorageSnapshot_ReplacesObjectsAndPreservesBytesAndMediaTypes() {
        await _storage.UploadAsync(_storageOptions.BucketAssets, "maps/a.zip", "application/zip", [0, 255, 42], default);
        await _storage.UploadAsync(_storageOptions.BucketUserData, "avatars/a.png", "image/png", [1, 2, 3], default);
        var backupOptions = Options.Create(new BackupOptions {
            MaintenanceMode = true, WorkDirectory = Path.Combine(_directory, "work"), MaxArchiveBytes = 1024 * 1024
        });
        using var workspace = new BackupWorkspace(backupOptions);
        await using var db = new BeatDashDbContext(new DbContextOptionsBuilder<BeatDashDbContext>()
            .UseNpgsql("Host=unused;Database=unused").Options);
        var service = new BackupService(new PostgresBackupDatabase(db), _backup, workspace, backupOptions, NullLogger<BackupService>.Instance);
        var archive = await service.ExportAsync(BackupScope.Storage, default);
        await _storage.UploadAsync(_storageOptions.BucketAssets, "maps/a.zip", "text/plain", [9], default);
        await _storage.UploadAsync(_storageOptions.BucketAssets, "extra", "text/plain", [8], default);

        await using (var source = File.OpenRead(archive))
            await service.RestoreAsync(source, BackupScope.Storage, default);

        Assert.Equal(new byte[] { 0, 255, 42 }, await _storage.DownloadAsync(_storageOptions.BucketAssets, "maps/a.zip", default));
        Assert.Equal("application/zip", await _storage.GetContentTypeAsync(_storageOptions.BucketAssets, "maps/a.zip", default));
        Assert.Equal(new byte[] { 1, 2, 3 }, await _storage.DownloadAsync(_storageOptions.BucketUserData, "avatars/a.png", default));
        Assert.Null(await _storage.DownloadAsync(_storageOptions.BucketAssets, "extra", default));
    }

    public void Dispose() {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
