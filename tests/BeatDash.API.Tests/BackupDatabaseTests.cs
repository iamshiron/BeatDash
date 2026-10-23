using Microsoft.EntityFrameworkCore;
using Shiron.BeatDash.API.Services.Backup;
using Shiron.BeatDash.DB;
using Xunit;

namespace Shiron.BeatDash.API.Tests;

public sealed class BackupDatabaseTests {
    [Fact]
    public void Tables_ApplicationModel_IncludesEveryTableInDependencyOrder() {
        using var db = new BeatDashDbContext(new DbContextOptionsBuilder<BeatDashDbContext>()
            .UseNpgsql("Host=unused;Database=unused").Options);
        var backup = new BackupDatabase(db);
        var tables = backup.Tables;
        Assert.Equal(db.Model.GetRelationalModel().Tables.Count(), tables.Length);
        Assert.Contains(tables, t => t.Name == "Users");
        Assert.Contains(tables, t => t.Name == "UserTokens");
        Assert.Contains(tables, t => t.Name == "UserRoles");
        Assert.Contains(tables, t => t.Name == "PlaySessionItemMotionFrames");
        Assert.Contains(tables, t => t.Name == "HealthProxyClients");
        foreach (var table in tables) {
            foreach (var foreignKey in table.ForeignKeyConstraints.Where(f => f.PrincipalTable != table))
                Assert.True(Array.IndexOf(tables, foreignKey.PrincipalTable) < Array.IndexOf(tables, table));
        }
        Assert.Equal(64, backup.SchemaHash.Length);
    }
}
