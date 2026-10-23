using Microsoft.EntityFrameworkCore;
using Shiron.BeatDash.DB;
using Xunit;

namespace Shiron.BeatDash.API.Tests;

/// <summary>Guards database model compatibility when updating EF Core and Npgsql.</summary>
public sealed class BeatDashDbContextTests {
    [Fact]
    public void HasPendingModelChanges_CurrentModel_MatchesMigrationSnapshot() {
        var options = new DbContextOptionsBuilder<BeatDashDbContext>()
            .UseNpgsql("Host=localhost;Database=beatdash_model_validation;Username=unused")
            .Options;
        using var context = new BeatDashDbContext(options);

        Assert.False(context.Database.HasPendingModelChanges());
    }
}
