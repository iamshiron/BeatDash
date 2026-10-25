using Shiron.BeatDash.DB;

namespace Shiron.BeatDash.API.Configuration;

/// <summary>Database engine selection and startup behavior.</summary>
public sealed class DatabaseOptions {
    /// <summary>The engine <c>ConnectionStrings:Default</c> points at.</summary>
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Postgres;
    /// <summary>Apply bundled migrations before seeding the administrator.</summary>
    public bool ApplyMigrationsOnStartup { get; set; }
}
