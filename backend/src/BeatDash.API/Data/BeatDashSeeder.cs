namespace Shiron.BeatDash.API.Data;

public static class Seeder {
    public static void SeedMockData(this BeatDashDbContext context) {
        if (context.PlaySessions.Any()) return;
    }
}
