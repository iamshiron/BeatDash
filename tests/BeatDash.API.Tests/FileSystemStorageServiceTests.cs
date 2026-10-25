using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.API.Services.Impl;
using Xunit;

namespace Shiron.BeatDash.API.Tests;

public sealed class FileSystemStorageServiceTests : IDisposable {
    private readonly string _root = Path.Combine(Path.GetTempPath(), "beatdash-fs-storage-tests", Guid.NewGuid().ToString("N"));
    private readonly FileSystemStorageService _storage;

    public FileSystemStorageServiceTests() {
        _storage = new FileSystemStorageService(Options.Create(new StorageOptions { RootDirectory = _root }));
    }

    [Fact]
    public async Task UploadAsync_NestedUnicodeKey_RoundTripsBytesAndContentType() {
        const string key = "covers/nested/a space + ü.png";
        var payload = RandomNumberGenerator.GetBytes(6 * 1024 * 1024);

        await _storage.UploadAsync("assets", key, "image/png", payload, default);

        Assert.Equal(payload, await _storage.DownloadAsync("assets", key, default));
        Assert.Equal("image/png", await _storage.GetContentTypeAsync("assets", key, default));
        Assert.True(File.Exists(Path.Combine(_root, "assets", "covers", "nested", "a space + ü.png")));
    }

    [Fact]
    public async Task DownloadAsync_MissingBucketOrObject_ReturnsNull() {
        Assert.Null(await _storage.DownloadAsync("assets", "missing.png", default));
        await _storage.EnsureBucketAsync("assets", default);
        await _storage.EnsureBucketAsync("assets", default);
        Assert.Null(await _storage.DownloadAsync("assets", "missing.png", default));
    }

    [Fact]
    public async Task UploadAsync_ExistingObject_ReplacesContentAndLeavesNoTemporaryFiles() {
        await _storage.UploadAsync("assets", "maps/a.zip", "application/zip", [1, 2, 3], default);
        await _storage.UploadAsync("assets", "maps/a.zip", "application/octet-stream", [], default);

        Assert.Equal(Array.Empty<byte>(), await _storage.DownloadAsync("assets", "maps/a.zip", default));
        Assert.Equal("application/octet-stream", await _storage.GetContentTypeAsync("assets", "maps/a.zip", default));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_root, ".tmp")));
    }

    [Fact]
    public async Task List_MultipleObjects_ReturnsOrdinalKeysAndSizesOfThatBucketOnly() {
        await _storage.UploadAsync("assets", "b/z", "text/plain", [1], default);
        await _storage.UploadAsync("assets", "a", "text/plain", [1, 2], default);
        await _storage.UploadAsync("assets", "B", "text/plain", [], default);
        await _storage.UploadAsync("user-data", "other", "text/plain", [1], default);

        Assert.Equal([("B", 0L), ("a", 2L), ("b/z", 1L)], _storage.List("assets"));
        Assert.Empty(_storage.List("missing"));
    }

    [Fact]
    public async Task Delete_ExistingObject_RemovesBytesAndMetadata() {
        await _storage.UploadAsync("assets", "a/b.png", "image/png", [1], default);

        _storage.Delete("assets", "a/b.png");
        _storage.Delete("assets", "a/b.png");

        Assert.Null(await _storage.DownloadAsync("assets", "a/b.png", default));
        Assert.Equal("application/octet-stream", await _storage.GetContentTypeAsync("assets", "a/b.png", default));
        Assert.Empty(_storage.List("assets"));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("a/../../escape")]
    [InlineData("/rooted")]
    [InlineData("a//b")]
    [InlineData("a\\b")]
    [InlineData("./a")]
    [InlineData("")]
    public async Task UploadAsync_UnsafeKey_IsRejected(string key) {
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.UploadAsync("assets", key, "text/plain", [1], default));
        Assert.False(Directory.Exists(Path.Combine(_root, "escape")));
    }

    [Theory]
    [InlineData("")]
    [InlineData(".metadata")]
    [InlineData("../assets")]
    [InlineData("a/b")]
    public async Task UploadAsync_UnsafeBucket_IsRejected(string bucket) =>
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.UploadAsync(bucket, "key", "text/plain", [1], default));

    public void Dispose() {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
