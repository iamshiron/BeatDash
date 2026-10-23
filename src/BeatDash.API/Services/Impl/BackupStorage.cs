using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.API.Services.Backup;
using Shiron.BeatDash.Data.Backup;

namespace Shiron.BeatDash.API.Services.Impl;

/// <summary>Backup access to the two configured application buckets.</summary>
public sealed class BackupStorage(IMinioClient minio, IStorageService storage, IOptions<StorageOptions> options) : IBackupStorage {
    private string Resolve(string bucket) => bucket switch {
        "assets" => options.Value.BucketAssets,
        "user-data" => options.Value.BucketUserData,
        _ => throw new InvalidDataException("Unknown logical storage bucket.")
    };

    /// <inheritdoc/>
    public async IAsyncEnumerable<BackupObjectDTO> ListAsync([EnumeratorCancellation] CancellationToken ct) {
        foreach (var logical in new[] { "assets", "user-data" }) {
            var bucket = Resolve(logical);
            if (!await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct)) continue;
            await foreach (var item in minio.ListObjectsEnumAsync(new ListObjectsArgs().WithBucket(bucket).WithRecursive(true), ct)) {
                var stat = await minio.StatObjectAsync(new StatObjectArgs().WithBucket(bucket).WithObject(item.Key), ct);
                yield return new BackupObjectDTO(logical, item.Key, stat.ContentType,
                    new BackupPayloadDTO("", stat.Size, ""));
            }
        }
    }

    /// <inheritdoc/>
    public async Task ReadAsync(BackupObjectDTO item, Stream destination, CancellationToken ct) =>
        await minio.GetObjectAsync(new GetObjectArgs().WithBucket(Resolve(item.Bucket)).WithObject(item.Key)
            .WithCallbackStream((stream, token) => stream.CopyToAsync(destination, token)), ct);

    /// <inheritdoc/>
    public async Task WriteAsync(BackupObjectDTO item, Stream source, CancellationToken ct) {
        var bucket = Resolve(item.Bucket);
        await storage.EnsureBucketAsync(bucket, ct);
        await minio.PutObjectAsync(new PutObjectArgs().WithBucket(bucket).WithObject(item.Key)
            .WithStreamData(source).WithObjectSize(item.Payload.Length).WithContentType(item.ContentType), ct);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(BackupObjectDTO item, CancellationToken ct) =>
        minio.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(Resolve(item.Bucket)).WithObject(item.Key), ct);
}
