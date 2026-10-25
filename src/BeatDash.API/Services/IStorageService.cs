namespace Shiron.BeatDash.API.Services;

/// <summary>
/// Provides object-storage operations (upload, download, bucket provisioning)
/// over the configured storage provider.
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
