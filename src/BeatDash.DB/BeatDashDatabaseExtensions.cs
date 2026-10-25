using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Shiron.BeatDash.DB;

/// <summary>Provider selection and initialization for <see cref="BeatDashDbContext"/>.</summary>
public static class BeatDashDatabaseExtensions {
    /// <summary>The assembly holding the SQLite migrations; PostgreSQL migrations live in this assembly.</summary>
    public const string SqliteMigrationsAssembly = "Shiron.BeatDash.DB.Sqlite";

    /// <summary>Configures the context for <paramref name="provider"/> and its migrations.</summary>
    public static DbContextOptionsBuilder UseBeatDashDatabase(this DbContextOptionsBuilder options, DatabaseProvider provider, string connectionString) =>
        provider switch {
            DatabaseProvider.Postgres => options.UseNpgsql(connectionString),
            DatabaseProvider.Sqlite => options.UseSqlite(connectionString, o => o.MigrationsAssembly(SqliteMigrationsAssembly)),
            _ => throw new InvalidOperationException($"Unsupported database provider '{provider}'.")
        };

    /// <summary>
    /// Optionally applies pending migrations, then applies engine settings that persist in the
    /// database. SQLite is switched to write-ahead logging so readers don't block the writer.
    /// </summary>
    public static async Task InitializeAsync(this DatabaseFacade database, bool applyMigrations, CancellationToken ct = default) {
        if (applyMigrations) await database.MigrateAsync(ct);
        if (database.IsSqlite()) await database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);
    }
}
