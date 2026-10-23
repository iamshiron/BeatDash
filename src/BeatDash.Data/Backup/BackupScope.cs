namespace Shiron.BeatDash.Data.Backup;

/// <summary>The application data included in a backup operation.</summary>
public enum BackupScope {
    /// <summary>All persisted application and Identity records.</summary>
    Database = 1,
    /// <summary>All objects in the application's assets and user-data buckets.</summary>
    Storage = 2,
    /// <summary>Database records and server-owned storage objects.</summary>
    Both = 3
}
