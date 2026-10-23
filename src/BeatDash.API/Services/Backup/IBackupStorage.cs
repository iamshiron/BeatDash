using Shiron.BeatDash.Data.Backup;

namespace Shiron.BeatDash.API.Services.Backup;

/// <summary>Streams only objects owned by this BeatDash installation.</summary>
public interface IBackupStorage {
    /// <summary>Lists every object in both logical application buckets.</summary>
    IAsyncEnumerable<BackupObjectDTO> ListAsync(CancellationToken ct);
    /// <summary>Copies an object into a destination stream.</summary>
    Task ReadAsync(BackupObjectDTO item, Stream destination, CancellationToken ct);
    /// <summary>Replaces an object from a stream.</summary>
    Task WriteAsync(BackupObjectDTO item, Stream source, CancellationToken ct);
    /// <summary>Removes an application object.</summary>
    Task DeleteAsync(BackupObjectDTO item, CancellationToken ct);
}
