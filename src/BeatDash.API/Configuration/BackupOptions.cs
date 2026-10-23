namespace Shiron.BeatDash.API.Configuration;

/// <summary>Server-owned backup workspace and maintenance configuration.</summary>
public sealed class BackupOptions {
    /// <summary>Start without writers and serve only backup administration and health checks.</summary>
    public bool MaintenanceMode { get; set; }
    /// <summary>Private, persistent workspace; never put this under the web root.</summary>
    public string WorkDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "backup-work");
    /// <summary>Maximum uploaded archive and total uncompressed payload size.</summary>
    public long MaxArchiveBytes { get; set; } = 1L << 40;
}
