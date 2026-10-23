using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.Data.Backup;

namespace Shiron.BeatDash.API.Services.Backup;

/// <summary>Coordinates complete application archives and durable restore recovery.</summary>
public sealed class BackupService(BackupDatabase database, IBackupStorage storage, BackupWorkspace workspace,
    IOptions<BackupOptions> options, ILogger<BackupService> logger) {
    /// <summary>Creates a private archive; the caller owns and must delete the returned file.</summary>
    public async Task<string> ExportAsync(BackupScope scope, CancellationToken ct) {
        CheckScope(scope);
        await workspace.Gate.WaitAsync(ct);
        try {
            CheckRecovery();
            return await CreateAsync(scope, ct);
        } finally { workspace.Gate.Release(); }
    }

    /// <summary>Validates and replaces the selected state, retaining a durable pre-restore backup until success.</summary>
    public async Task<BackupRestoreResponse> RestoreAsync(Stream source, BackupScope scope, CancellationToken ct) {
        CheckScope(scope);
        var path = workspace.NewPath();
        await workspace.Gate.WaitAsync(ct);
        try {
            CheckRecovery();
            await using (var target = File.Create(path)) await CopyLimitedAsync(source, target, ct);
            using var archive = ZipFile.OpenRead(path);
            var manifest = await ValidateAsync(archive, scope, ct);
            var recovery = await CreateAsync(scope, ct);
            File.Move(recovery, workspace.RecoveryPath);
            // Once replacement starts, disconnecting the CLI must not interrupt recovery bookkeeping.
            await ApplyAsync(archive, manifest, scope, CancellationToken.None);
            File.Delete(workspace.RecoveryPath);
            logger.LogInformation("Restored BeatDash backup scope {Scope}", scope);
            return Result(manifest, scope);
        } finally {
            workspace.Gate.Release();
            File.Delete(path);
        }
    }

    /// <summary>Restores the durable pre-operation snapshot after a failed or interrupted restore.</summary>
    public async Task<BackupRestoreResponse> RecoverAsync(CancellationToken ct) {
        CheckScope(BackupScope.Both);
        await workspace.Gate.WaitAsync(ct);
        try {
            if (!File.Exists(workspace.RecoveryPath)) throw new InvalidOperationException("No restore recovery is pending.");
            BackupManifest manifest;
            using (var archive = ZipFile.OpenRead(workspace.RecoveryPath)) {
                manifest = await BackupArchive.ValidateAsync(archive, options.Value.MaxArchiveBytes, ct);
                await ValidateCompatibilityAsync(manifest, manifest.Scope, ct);
                await ApplyAsync(archive, manifest, manifest.Scope, CancellationToken.None);
            }
            File.Delete(workspace.RecoveryPath);
            logger.LogInformation("Recovered pre-restore BeatDash state");
            return Result(manifest, manifest.Scope);
        } finally { workspace.Gate.Release(); }
    }

    private void CheckScope(BackupScope scope) {
        if (!options.Value.MaintenanceMode) throw new InvalidOperationException("Restart BeatDash in backup maintenance mode first.");
        if (!Enum.IsDefined(scope)) throw new InvalidDataException("Invalid backup scope.");
    }

    private void CheckRecovery() {
        if (File.Exists(workspace.RecoveryPath))
            throw new InvalidOperationException("A restore was interrupted. Run backup recover before another operation.");
    }

    private async Task<string> CreateAsync(BackupScope scope, CancellationToken ct) {
        var path = workspace.NewPath();
        try {
            using (var archive = ZipFile.Open(path, ZipArchiveMode.Create)) {
                var tables = new List<BackupTableDTO>();
                var objects = new List<BackupObjectDTO>();
                BackupSequenceDTO[] sequences = [];
                var schema = "";
                var major = 0;
                long total = 0;
                async Task<BackupPayloadDTO> AddAsync(Func<Stream, Task> write) {
                    await using var buffer = new FileStream(workspace.NewPath(), FileMode.CreateNew, FileAccess.ReadWrite,
                        FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
                    await write(buffer);
                    if (buffer.Length > options.Value.MaxArchiveBytes - total)
                        throw new InvalidDataException("Backup exceeds configured size limit.");
                    total += buffer.Length;
                    buffer.Position = 0;
                    var hash = Convert.ToHexString(await SHA256.HashDataAsync(buffer, ct));
                    buffer.Position = 0;
                    var entry = archive.CreateEntry($"payload/{tables.Count + objects.Count:D8}", CompressionLevel.Fastest);
                    await using var output = entry.Open();
                    await buffer.CopyToAsync(output, ct);
                    return new BackupPayloadDTO(entry.FullName, buffer.Length, hash);
                }
                if (scope is BackupScope.Database or BackupScope.Both) {
                    schema = database.SchemaHash;
                    major = await database.GetMajorVersionAsync(ct);
                    sequences = await database.ExportAsync(async (name, input, token) =>
                        tables.Add(new BackupTableDTO(name, await AddAsync(output => input.CopyToAsync(output, token)))), ct);
                }
                if (scope is BackupScope.Storage or BackupScope.Both) {
                    await foreach (var item in storage.ListAsync(ct))
                        objects.Add(item with { Payload = await AddAsync(output => storage.ReadAsync(item, output, ct)) });
                }
                var manifest = new BackupManifest(BackupArchive.FormatVersion, DateTimeOffset.UtcNow, scope,
                    schema, major, tables.ToArray(), objects.ToArray(), sequences);
                await using var manifestStream = archive.CreateEntry(BackupArchive.ManifestEntry).Open();
                await JsonSerializer.SerializeAsync(manifestStream, manifest, cancellationToken: ct);
            }
            using (var archive = ZipFile.OpenRead(path))
                await BackupArchive.ValidateAsync(archive, options.Value.MaxArchiveBytes, ct);
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
                file.Flush(flushToDisk: true);
            return path;
        } catch {
            File.Delete(path);
            throw;
        }
    }

    private async Task<BackupManifest> ValidateAsync(ZipArchive archive, BackupScope scope, CancellationToken ct) {
        var manifest = await BackupArchive.ValidateAsync(archive, options.Value.MaxArchiveBytes, ct);
        await ValidateCompatibilityAsync(manifest, scope, ct);
        return manifest;
    }

    private async Task ValidateCompatibilityAsync(BackupManifest manifest, BackupScope scope, CancellationToken ct) {
        if ((manifest.Scope & scope) != scope) throw new InvalidDataException("Requested scope is not present in the archive.");
        if (scope is not (BackupScope.Database or BackupScope.Both)) return;
        if (manifest.SchemaHash != database.SchemaHash || manifest.PostgresMajorVersion != await database.GetMajorVersionAsync(ct)
            || !manifest.Tables.Select(t => t.Name).Order().SequenceEqual(database.Tables.Select(BackupDatabase.TableName).Order())
            || !manifest.Sequences.Select(s => s.Name).SequenceEqual((await database.GetSequencesAsync(ct)).Select(s => s.Name)))
            throw new InvalidDataException("Backup requires the same application database schema and PostgreSQL major version.");
    }

    private async Task ApplyAsync(ZipArchive archive, BackupManifest manifest, BackupScope scope, CancellationToken ct) {
        if (scope is BackupScope.Storage or BackupScope.Both) {
            foreach (var item in manifest.Objects) {
                await using var input = archive.GetEntry(item.Payload.Entry)!.Open();
                await storage.WriteAsync(item, input, ct);
            }
            var wanted = manifest.Objects.Select(o => (o.Bucket, o.Key)).ToHashSet();
            var extra = new List<BackupObjectDTO>();
            await foreach (var item in storage.ListAsync(ct))
                if (!wanted.Contains((item.Bucket, item.Key))) extra.Add(item);
            foreach (var item in extra) await storage.DeleteAsync(item, ct);
        }
        if (scope is BackupScope.Database or BackupScope.Both)
            await database.RestoreAsync(manifest, payload => archive.GetEntry(payload.Entry)!.Open(), ct);
    }

    private async Task CopyLimitedAsync(Stream source, Stream target, CancellationToken ct) {
        var buffer = new byte[81920];
        long length = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) != 0) {
            length += read;
            if (length > options.Value.MaxArchiveBytes) throw new InvalidDataException("Archive exceeds configured size limit.");
            await target.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        await target.FlushAsync(ct);
    }

    private static BackupRestoreResponse Result(BackupManifest manifest, BackupScope scope) => new(scope,
        scope is BackupScope.Database or BackupScope.Both ? manifest.Tables.Length : 0,
        scope is BackupScope.Storage or BackupScope.Both ? manifest.Objects.Length : 0);
}
