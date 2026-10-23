using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.API.Endpoints;
using Xunit;

namespace Shiron.BeatDash.API.Tests;

public sealed class HspEndpointsTests {
    [Fact]
    public void GetHspIngestUrl_PublicUrlConfigured_ReturnsConfiguredHttpsProxyUrl() {
        var options = new HspOptions {
            PublicIngestUrl = "https://dash.example/beatdash/api/hsp/ingest"
        };

        var result = ServerInfoEndpoints.GetHspIngestUrl(options, "192.168.1.10", 5000);

        Assert.Equal("https://dash.example/beatdash/api/hsp/ingest", result);
    }

    [Fact]
    public void GetHspIngestUrl_PublicUrlNotConfigured_ReturnsLanHttpUrl() {
        var result = ServerInfoEndpoints.GetHspIngestUrl(new HspOptions(), "192.168.1.10", 5000);

        Assert.Equal("http://192.168.1.10:5000/api/hsp/ingest", result);
    }

    [Fact]
    public void IsWithinPlayWindow_ActiveSessionSample_ReturnsTrue() {
        var startedAt = new DateTime(2026, 10, 23, 10, 0, 0, DateTimeKind.Utc);
        var batchLatest = startedAt.AddMinutes(3);

        var result = HspEndpoints.IsWithinPlayWindow(startedAt.AddMinutes(2), startedAt, batchLatest);

        Assert.True(result);
    }

    [Fact]
    public void IsWithinPlayWindow_SampleOutsideWindow_ReturnsFalse() {
        var startedAt = new DateTime(2026, 10, 23, 10, 0, 0, DateTimeKind.Utc);
        var endedAt = startedAt.AddMinutes(3);

        var result = HspEndpoints.IsWithinPlayWindow(endedAt.AddTicks(1), startedAt, endedAt);

        Assert.False(result);
    }
}
