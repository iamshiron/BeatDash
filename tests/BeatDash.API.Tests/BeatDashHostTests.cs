using Microsoft.Extensions.Configuration;
using Xunit;

namespace Shiron.BeatDash.API.Tests;

public sealed class BeatDashHostTests {
    [Theory]
    [InlineData("")]
    [InlineData("too-short-session-token")]
    public void Build_DesktopModeWithWeakSessionToken_IsRejected(string token) {
        var error = Assert.Throws<InvalidOperationException>(() => Build(new() { ["Desktop:SessionToken"] = token }));

        Assert.Contains("session token", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_DesktopModeInBackupMaintenance_IsRejected() {
        var error = Assert.Throws<InvalidOperationException>(() => Build(new() {
            ["Desktop:SessionToken"] = new string('x', 32), ["Backup:MaintenanceMode"] = "true"
        }));

        Assert.Contains("maintenance", error.Message, StringComparison.Ordinal);
    }

    private static void Build(Dictionary<string, string?> settings) {
        var builder = BeatDashHost.CreateBuilder([]);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["Jwt:SecretKey"] = "beatdash-host-test-secret-at-least-32-characters",
            ["Jwt:Issuer"] = "test", ["Jwt:Audience"] = "test",
            ["Desktop:Enabled"] = "true",
            ["Backup:WorkDirectory"] = Path.Combine(Path.GetTempPath(), "beatdash-host-tests")
        }).AddInMemoryCollection(settings);
        BeatDashHost.Build(builder);
    }
}
