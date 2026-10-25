namespace Shiron.BeatDash.DB;

/// <summary>The relational database engine backing <see cref="BeatDashDbContext"/>.</summary>
public enum DatabaseProvider {
    /// <summary>PostgreSQL through Npgsql. The default and first-class engine.</summary>
    Postgres,
    /// <summary>An embedded SQLite file, for single-machine installations.</summary>
    Sqlite
}
