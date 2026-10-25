using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Shiron.BeatDash.DB.Sqlite;

/// <summary>
/// Lets <c>dotnet ef</c> target this project directly for SQLite migrations:
/// <c>dotnet ef migrations add &lt;Name&gt; --project src/BeatDash.DB.Sqlite --startup-project src/BeatDash.DB.Sqlite</c>.
/// </summary>
public sealed class SqliteDesignTimeDbContextFactory : IDesignTimeDbContextFactory<BeatDashDbContext> {
    /// <inheritdoc/>
    public BeatDashDbContext CreateDbContext(string[] args) {
        var options = new DbContextOptionsBuilder<BeatDashDbContext>();
        options.UseBeatDashDatabase(DatabaseProvider.Sqlite, "Data Source=beatdash-design-time.db");
        return new BeatDashDbContext(options.Options);
    }
}
