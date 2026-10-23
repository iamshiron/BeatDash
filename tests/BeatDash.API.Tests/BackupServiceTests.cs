using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.API.Services.Backup;
using Shiron.BeatDash.Data.Backup;
using Shiron.BeatDash.DB;
using Xunit;

namespace Shiron.BeatDash.API.Tests;

public sealed class BackupServiceTests {
    [Fact]
    public async Task Restore_StorageSnapshot_ReplacesObjectsAndPreservesBytesAndMediaTypes() {
        using var fixture = new Fixture();
        fixture.Storage.Put("assets", "maps/a.zip", [0, 255, 42], "application/zip");
        fixture.Storage.Put("user-data", "avatars/a.png", [1, 2, 3], "image/png");
        var archive = await fixture.Service.ExportAsync(BackupScope.Storage, default);
        fixture.Storage.Put("assets", "maps/a.zip", [9], "text/plain");
        fixture.Storage.Put("assets", "extra", [8], "text/plain");
        await using var source = File.OpenRead(archive);

        var result = await fixture.Service.RestoreAsync(source, BackupScope.Storage, default);

        Assert.Equal(2, result.Objects);
        Assert.Equal(0, result.Tables);
        Assert.Equal(2, fixture.Storage.Items.Count);
        Assert.Equal(new byte[] { 0, 255, 42 }, fixture.Storage.Items[("assets", "maps/a.zip")].Bytes);
        Assert.Equal("application/zip", fixture.Storage.Items[("assets", "maps/a.zip")].ContentType);
        Assert.Equal(new byte[] { 1, 2, 3 }, fixture.Storage.Items[("user-data", "avatars/a.png")].Bytes);
        Assert.False(File.Exists(Path.Combine(fixture.Directory, "recovery.zip")));
    }

    [Fact]
    public async Task Restore_InterruptedStorageWrite_RequiresAndSupportsDurableRecovery() {
        using var fixture = new Fixture();
        fixture.Storage.Put("assets", "a", [1], "application/octet-stream");
        fixture.Storage.Put("assets", "b", [2], "application/octet-stream");
        var archive = await fixture.Service.ExportAsync(BackupScope.Storage, default);
        fixture.Storage.Put("assets", "a", [3], "image/png");
        fixture.Storage.Put("assets", "b", [4], "image/png");
        fixture.Storage.FailOnWrite = 2;
        await using var source = File.OpenRead(archive);

        await Assert.ThrowsAsync<IOException>(() => fixture.Service.RestoreAsync(source, BackupScope.Storage, default));
        Assert.True(File.Exists(Path.Combine(fixture.Directory, "recovery.zip")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.ExportAsync(BackupScope.Storage, default));

        // A fresh coordinator simulates a process restart; only the durable archive carries recovery state.
        using var restartedWorkspace = new BackupWorkspace(fixture.Options);
        var restarted = fixture.CreateService(restartedWorkspace);
        await restarted.RecoverAsync(default);

        Assert.Equal(new byte[] { 3 }, fixture.Storage.Items[("assets", "a")].Bytes);
        Assert.Equal(new byte[] { 4 }, fixture.Storage.Items[("assets", "b")].Bytes);
        Assert.Equal("image/png", fixture.Storage.Items[("assets", "a")].ContentType);
        Assert.False(File.Exists(Path.Combine(fixture.Directory, "recovery.zip")));
    }

    [Fact]
    public async Task Restore_CorruptedPayload_DoesNotChangeStorage() {
        using var fixture = new Fixture();
        fixture.Storage.Put("assets", "a", [1], "text/plain");
        var archive = await fixture.Service.ExportAsync(BackupScope.Storage, default);
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Update)) {
            using var payload = zip.GetEntry("payload/00000000")!.Open();
            payload.WriteByte(2);
        }
        await using var source = File.OpenRead(archive);

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Service.RestoreAsync(source, BackupScope.Storage, default));

        Assert.Equal(0, fixture.Storage.Writes);
        Assert.Equal(new byte[] { 1 }, fixture.Storage.Items[("assets", "a")].Bytes);
        Assert.False(File.Exists(Path.Combine(fixture.Directory, "recovery.zip")));
    }

    [Fact]
    public async Task Restore_MissingRequestedScope_DoesNotChangeStorage() {
        using var fixture = new Fixture();
        var archive = await fixture.Service.ExportAsync(BackupScope.Storage, default);
        await using var source = File.OpenRead(archive);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Service.RestoreAsync(source, BackupScope.Both, default));
        Assert.Equal(0, fixture.Storage.Writes);
    }

    [Fact]
    public async Task Export_OutsideMaintenance_IsRejected() {
        using var fixture = new Fixture();
        fixture.Options.Value.MaintenanceMode = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.ExportAsync(BackupScope.Storage, default));
    }

    [Fact]
    public async Task Validate_DuplicateEntries_IsRejected() {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true)) {
            zip.CreateEntry("manifest.json");
            zip.CreateEntry("manifest.json");
        }
        buffer.Position = 0;
        using var archive = new ZipArchive(buffer, ZipArchiveMode.Read);
        await Assert.ThrowsAsync<InvalidDataException>(() => BackupArchive.ValidateAsync(archive, 1024, default));
    }

    [Fact]
    public async Task Validate_UnknownBucket_IsRejected() {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true)) {
            await using var output = zip.CreateEntry("manifest.json").Open();
            await JsonSerializer.SerializeAsync(output, new BackupManifest(1, DateTimeOffset.UtcNow, BackupScope.Storage,
                "", 0, [], [new BackupObjectDTO("external", "a", "text/plain", new BackupPayloadDTO("payload/0", 0, ""))], []));
        }
        buffer.Position = 0;
        using var archive = new ZipArchive(buffer, ZipArchiveMode.Read);
        await Assert.ThrowsAsync<InvalidDataException>(() => BackupArchive.ValidateAsync(archive, 4096, default));
    }

    [Fact]
    public async Task Restore_EmptyStorageArchive_RemovesAllExistingObjects() {
        using var fixture = new Fixture();
        var archive = await fixture.Service.ExportAsync(BackupScope.Storage, default);
        fixture.Storage.Put("user-data", "a", Encoding.UTF8.GetBytes("later"), "text/plain");
        await using var source = File.OpenRead(archive);
        await fixture.Service.RestoreAsync(source, BackupScope.Storage, default);
        Assert.Empty(fixture.Storage.Items);
    }

    private sealed class Fixture : IDisposable {
        public string Directory { get; } = Path.Combine(Path.GetTempPath(), "beatdash-backup-tests", Guid.NewGuid().ToString("N"));
        public IOptions<BackupOptions> Options { get; }
        public MemoryStorage Storage { get; } = new();
        private readonly BeatDashDbContext _db = new(new DbContextOptionsBuilder<BeatDashDbContext>()
            .UseNpgsql("Host=unused;Database=unused").Options);
        private readonly BackupWorkspace _workspace;
        public BackupService Service { get; }

        public Fixture() {
            Options = Microsoft.Extensions.Options.Options.Create(new BackupOptions {
                MaintenanceMode = true, WorkDirectory = Directory, MaxArchiveBytes = 1024 * 1024
            });
            _workspace = new BackupWorkspace(Options);
            Service = CreateService(_workspace);
        }
        public BackupService CreateService(BackupWorkspace workspace) => new(new BackupDatabase(_db), Storage,
            workspace, Options, NullLogger<BackupService>.Instance);
        public void Dispose() {
            _workspace.Dispose();
            _db.Dispose();
            if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, true);
        }
    }

    private sealed class MemoryStorage : IBackupStorage {
        public Dictionary<(string Bucket, string Key), (byte[] Bytes, string ContentType)> Items { get; } = [];
        public int Writes { get; private set; }
        public int FailOnWrite { get; set; }
        public void Put(string bucket, string key, byte[] bytes, string contentType) => Items[(bucket, key)] = (bytes, contentType);
        public async IAsyncEnumerable<BackupObjectDTO> ListAsync([EnumeratorCancellation] CancellationToken ct) {
            await Task.CompletedTask;
            foreach (var (key, value) in Items)
                yield return new BackupObjectDTO(key.Bucket, key.Key, value.ContentType, new BackupPayloadDTO("", value.Bytes.Length, ""));
        }
        public Task ReadAsync(BackupObjectDTO item, Stream destination, CancellationToken ct) =>
            destination.WriteAsync(Items[(item.Bucket, item.Key)].Bytes, ct).AsTask();
        public async Task WriteAsync(BackupObjectDTO item, Stream source, CancellationToken ct) {
            if (++Writes == FailOnWrite) throw new IOException("Injected storage failure.");
            using var buffer = new MemoryStream();
            await source.CopyToAsync(buffer, ct);
            Put(item.Bucket, item.Key, buffer.ToArray(), item.ContentType);
        }
        public Task DeleteAsync(BackupObjectDTO item, CancellationToken ct) {
            Items.Remove((item.Bucket, item.Key));
            return Task.CompletedTask;
        }
    }
}
