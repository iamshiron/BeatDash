namespace Shiron.BeatDash.Data.Backup;

/// <summary>The database engine whose native format a backup's table payloads use.</summary>
public enum BackupDatabaseEngine {
    /// <summary>PostgreSQL binary COPY payloads. Archives created before engines were recorded are PostgreSQL archives.</summary>
    Postgres = 0,
    /// <summary>SQLite storage-class row payloads.</summary>
    Sqlite = 1
}
