namespace Shiron.BeatDash.API.Configuration;

/// <summary>The object storage backend holding map assets and user uploads.</summary>
public enum StorageProvider {
    /// <summary>RustFS (or any S3-compatible server) through the MinIO client. The default.</summary>
    RustFs,
    /// <summary>Plain directories on the local file system, for single-machine installations.</summary>
    FileSystem
}
