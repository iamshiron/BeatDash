using Microsoft.Extensions.Options;
using Minio;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.API.Services;
using Shiron.BeatDash.API.Services.Backup;
using Shiron.BeatDash.API.Services.Impl;

namespace Shiron.BeatDash.API.Hosting;

/// <summary>Registers the configured object storage backend.</summary>
public static class StorageHostingExtensions {
    /// <summary>Registers <see cref="IStorageService"/> and <see cref="IBackupStorage"/> for <paramref name="provider"/>.</summary>
    public static IServiceCollection AddStorage(this IServiceCollection services, StorageProvider provider) {
        switch (provider) {
            case StorageProvider.RustFs:
                // RustFS uses the existing S3-compatible client, including backup streaming.
                services.AddSingleton<IMinioClient>(sp => {
                    var opts = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
                    return new MinioClient()
                        .WithEndpoint(opts.Endpoint)
                        .WithCredentials(opts.AccessKey, opts.SecretKey)
                        .WithSSL(opts.UseSsl)
                        .Build();
                });
                services.AddSingleton<IStorageService, MinioStorageService>();
                services.AddSingleton<IBackupStorage, MinioBackupStorage>();
                return services;
            case StorageProvider.FileSystem:
                services.AddSingleton<FileSystemStorageService>();
                services.AddSingleton<IStorageService>(sp => sp.GetRequiredService<FileSystemStorageService>());
                services.AddSingleton<IBackupStorage, FileSystemBackupStorage>();
                return services;
            default:
                throw new InvalidOperationException($"Unsupported storage provider '{provider}'.");
        }
    }
}
