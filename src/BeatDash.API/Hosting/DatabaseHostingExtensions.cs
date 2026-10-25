using Shiron.BeatDash.API.Services.Backup;
using Shiron.BeatDash.DB;

namespace Shiron.BeatDash.API.Hosting;

/// <summary>Registers the configured database engine.</summary>
public static class DatabaseHostingExtensions {
    /// <summary>Registers <see cref="BeatDashDbContext"/>, its factory and the engine's <see cref="BackupDatabase"/>.</summary>
    public static IServiceCollection AddDatabase(this IServiceCollection services, DatabaseProvider provider, string connectionString) {
        services.AddDbContextFactory<BeatDashDbContext>(options => options.UseBeatDashDatabase(provider, connectionString));
        switch (provider) {
            case DatabaseProvider.Postgres:
                services.AddScoped<BackupDatabase, PostgresBackupDatabase>();
                return services;
            case DatabaseProvider.Sqlite:
                services.AddScoped<BackupDatabase, SqliteBackupDatabase>();
                return services;
            default:
                throw new InvalidOperationException($"Unsupported database provider '{provider}'.");
        }
    }
}
