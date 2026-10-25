using System.Text;
using Microsoft.Extensions.Options;
using Shiron.BeatDash.API.Configuration;

namespace Shiron.BeatDash.API.Services.Impl;

/// <summary>
/// Local file-system implementation. Each bucket is a directory below
/// <see cref="StorageOptions.RootDirectory"/> and each object key a relative path inside it;
/// content types live in a parallel metadata tree. Writes go to a temporary file first and
/// are renamed into place, so readers never observe partially written objects.
/// </summary>
public sealed class FileSystemStorageService(IOptions<StorageOptions> options) : IStorageService {
    private const string MetadataDirectory = ".metadata";
    private const string TemporaryDirectory = ".tmp";
    private const string DefaultContentType = "application/octet-stream";
    private const int BufferSize = 81920;

    private readonly string _root = Path.GetFullPath(options.Value.RootDirectory);

    /// <inheritdoc/>
    public Task EnsureBucketAsync(string bucket, CancellationToken ct) {
        Directory.CreateDirectory(BucketPath(bucket));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task UploadAsync(string bucket, string objectKey, string contentType, byte[] data, CancellationToken ct) {
        using var stream = new MemoryStream(data, writable: false);
        await WriteAsync(bucket, objectKey, contentType, stream, ct);
    }

    /// <inheritdoc/>
    public async Task<byte[]?> DownloadAsync(string bucket, string objectKey, CancellationToken ct) {
        try {
            return await File.ReadAllBytesAsync(ObjectPath(bucket, objectKey), ct);
        } catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) {
            return null;
        }
    }

    /// <summary>Replaces an object with the content of <paramref name="source"/>.</summary>
    public async Task WriteAsync(string bucket, string objectKey, string contentType, Stream source, CancellationToken ct) {
        var path = ObjectPath(bucket, objectKey);
        var metadataPath = MetadataPath(bucket, objectKey);
        await ReplaceAsync(path, output => source.CopyToAsync(output, ct), ct);
        await ReplaceAsync(metadataPath, output => output.WriteAsync(Encoding.UTF8.GetBytes(contentType), ct).AsTask(), ct);
    }

    /// <summary>Opens an object for reading.</summary>
    public FileStream OpenRead(string bucket, string objectKey) =>
        new(ObjectPath(bucket, objectKey), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, BufferSize, FileOptions.Asynchronous);

    /// <summary>The stored content type of an object, or <c>application/octet-stream</c> if none was recorded.</summary>
    public async Task<string> GetContentTypeAsync(string bucket, string objectKey, CancellationToken ct) {
        try {
            var contentType = await File.ReadAllTextAsync(MetadataPath(bucket, objectKey), ct);
            return string.IsNullOrEmpty(contentType) ? DefaultContentType : contentType;
        } catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) {
            return DefaultContentType;
        }
    }

    /// <summary>Lists every object key and size in a bucket, in ordinal key order.</summary>
    public (string Key, long Length)[] List(string bucket) {
        var directory = BucketPath(bucket);
        if (!Directory.Exists(directory)) return [];
        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(path => (Key: Path.GetRelativePath(directory, path).Replace(Path.DirectorySeparatorChar, '/'),
                Length: new FileInfo(path).Length))
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>Removes an object and its metadata; missing objects are ignored.</summary>
    public void Delete(string bucket, string objectKey) {
        File.Delete(ObjectPath(bucket, objectKey));
        File.Delete(MetadataPath(bucket, objectKey));
    }

    private async Task ReplaceAsync(string path, Func<Stream, Task> write, CancellationToken ct) {
        var temporary = Path.Combine(Directory.CreateDirectory(Path.Combine(_root, TemporaryDirectory)).FullName, Guid.NewGuid().ToString("N"));
        try {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous)) {
                await write(output);
                await output.FlushAsync(ct);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.Move(temporary, path, overwrite: true);
        } finally {
            File.Delete(temporary);
        }
    }

    private string BucketPath(string bucket) => Path.Combine(_root, CheckBucket(bucket));

    private string ObjectPath(string bucket, string objectKey) => Resolve(BucketPath(bucket), objectKey);

    private string MetadataPath(string bucket, string objectKey) =>
        Resolve(Path.Combine(_root, MetadataDirectory, CheckBucket(bucket)), objectKey);

    private static string CheckBucket(string bucket) {
        if (string.IsNullOrEmpty(bucket) || bucket.StartsWith('.') || bucket.IndexOfAny(['/', '\\']) >= 0
            || bucket.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException($"Invalid storage bucket name '{bucket}'.", nameof(bucket));
        return bucket;
    }

    private static string Resolve(string directory, string objectKey) {
        var segments = objectKey.Split('/');
        if (objectKey.Contains('\\') || segments.Any(s => s.Length == 0 || s is "." or ".."
                || s.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            throw new ArgumentException($"Invalid storage object key '{objectKey}'.", nameof(objectKey));
        var path = Path.GetFullPath(Path.Combine([directory, .. segments]));
        if (!path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException($"Invalid storage object key '{objectKey}'.", nameof(objectKey));
        return path;
    }
}
