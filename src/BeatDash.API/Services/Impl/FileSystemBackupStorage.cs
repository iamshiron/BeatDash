using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.API.Services.Backup;
using Shiron.BeatDash.Data.Backup;

namespace Shiron.BeatDash.API.Services.Impl;

/// <summary>Backup access to the two configured application bucket directories.</summary>
public sealed class FileSystemBackupStorage(FileSystemStorageService storage, IOptions<StorageOptions> options) : IBackupStorage {
    private string Resolve(string bucket) => bucket switch {
        "assets" => options.Value.BucketAssets,
        "user-data" => options.Value.BucketUserData,
        _ => throw new InvalidDataException("Unknown logical storage bucket.")
    };

    /// <inheritdoc/>
    public async IAsyncEnumerable<BackupObjectDTO> ListAsync([EnumeratorCancellation] CancellationToken ct) {
        foreach (var logical in new[] { "assets", "user-data" }) {
            var bucket = Resolve(logical);
            foreach (var (key, length) in storage.List(bucket)) {
                yield return new BackupObjectDTO(logical, key, await storage.GetContentTypeAsync(bucket, key, ct),
                    new BackupPayloadDTO("", length, ""));
            }
        }
    }

    /// <inheritdoc/>
    public async Task ReadAsync(BackupObjectDTO item, Stream destination, CancellationToken ct) {
        await using var source = storage.OpenRead(Resolve(item.Bucket), item.Key);
        await source.CopyToAsync(destination, ct);
    }

    /// <inheritdoc/>
    public Task WriteAsync(BackupObjectDTO item, Stream source, CancellationToken ct) =>
        storage.WriteAsync(Resolve(item.Bucket), item.Key, item.ContentType, source, ct);

    /// <inheritdoc/>
    public Task DeleteAsync(BackupObjectDTO item, CancellationToken ct) {
        storage.Delete(Resolve(item.Bucket), item.Key);
        return Task.CompletedTask;
    }
}
