using System.Collections.Concurrent;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace Shiron.BeatDash.API.Services;

/// <summary>
/// Provides object-storage operations (upload, download, bucket provisioning)
/// over a backing <see cref="IMinioClient"/>.
/// </summary>
public interface IStorageService {
    /// <summary>
    /// Ensures a bucket exists, creating it if necessary.
    /// </summary>
    Task EnsureBucketAsync(string bucket, CancellationToken ct);

    /// <summary>
    /// Uploads byte content to an object, overwriting if it already exists.
    /// </summary>
    Task UploadAsync(string bucket, string objectKey, string contentType, byte[] data, CancellationToken ct);

    /// <summary>
    /// Downloads an object's bytes, or <see langword="null"/> if it (or its bucket) is missing.
    /// </summary>
    Task<byte[]?> DownloadAsync(string bucket, string objectKey, CancellationToken ct);
}

/// <summary>
/// <see cref="IMinioClient"/>-backed implementation. Buckets that have already
/// been verified/created are remembered to avoid repeated existence checks.
/// </summary>
public sealed class MinioStorageService(IMinioClient minio, ILogger<MinioStorageService> logger) : IStorageService {
    private readonly ConcurrentDictionary<string, bool> _ensuredBuckets = new();

    /// <inheritdoc/>
    public async Task EnsureBucketAsync(string bucket, CancellationToken ct) {
        if (_ensuredBuckets.ContainsKey(bucket)) return;

        var exists = await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct);
        if (!exists) {
            await minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), ct);
            logger.LogInformation("Created bucket '{Bucket}'", bucket);
        }

        _ensuredBuckets.TryAdd(bucket, true);
    }

    /// <inheritdoc/>
    public async Task UploadAsync(string bucket, string objectKey, string contentType, byte[] data, CancellationToken ct) {
        await EnsureBucketAsync(bucket, ct);

        using var stream = new MemoryStream(data, writable: false);
        await minio.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectKey)
            .WithStreamData(stream)
            .WithObjectSize(data.LongLength)
            .WithContentType(contentType), ct);
    }

    /// <inheritdoc/>
    public async Task<byte[]?> DownloadAsync(string bucket, string objectKey, CancellationToken ct) {
        using var ms = new MemoryStream();
        try {
            await minio.GetObjectAsync(new GetObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectKey)
                .WithCallbackStream(async (stream, token) => await stream.CopyToAsync(ms, token)), ct);
        } catch (ObjectNotFoundException) {
            return null;
        } catch (BucketNotFoundException) {
            return null;
        }

        return ms.ToArray();
    }
}
