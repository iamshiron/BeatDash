using System;

namespace Shiron.BeatDash.Data.Backup;

/// <summary>Credentials for a short-lived, admin-only backup session.</summary>
public sealed record BackupLoginRequest(string Email, string Password, string TwoFactorCode = null);
/// <summary>A bearer token restricted to backup administration.</summary>
public sealed record BackupLoginResponse(string AccessToken, DateTimeOffset ExpiresAt);
/// <summary>Selection for an export operation.</summary>
public sealed record BackupExportRequest(BackupScope Scope);
/// <summary>Scope selected when uploading an archive to restore.</summary>
public sealed record BackupRestoreRequest(BackupScope Scope);
/// <summary>An actionable backup operation error.</summary>
public sealed record BackupErrorResponse(string Error);
/// <summary>Result of restoring the selected archive components.</summary>
public sealed record BackupRestoreResponse(BackupScope Scope, int Tables, int Objects);
/// <summary>Versioned archive inventory. Payload paths are opaque archive entry identifiers.</summary>
public sealed record BackupManifest(int FormatVersion, DateTimeOffset CreatedAt, BackupScope Scope,
    string SchemaHash, int PostgresMajorVersion, BackupTableDTO[] Tables, BackupObjectDTO[] Objects,
    BackupSequenceDTO[] Sequences);
/// <summary>A complete table payload with an integrity digest.</summary>
public sealed record BackupTableDTO(string Name, BackupPayloadDTO Payload);
/// <summary>An object in a logical application bucket, including its media type.</summary>
public sealed record BackupObjectDTO(string Bucket, string Key, string ContentType, BackupPayloadDTO Payload);
/// <summary>Payload size and SHA-256 integrity metadata.</summary>
public sealed record BackupPayloadDTO(string Entry, long Length, string Sha256);
/// <summary>State of a database-generated identifier sequence.</summary>
public sealed record BackupSequenceDTO(string Name, long LastValue, bool IsCalled);
