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

        var result = ServerInfoEndpoints.GetHspIngestUrl(options, new Uri("http://0.0.0.0:5000"), "192.168.1.10");

        Assert.Equal("https://dash.example/beatdash/api/hsp/ingest", result);
    }

    [Fact]
    public void GetHspIngestUrl_PublicUrlWhitespace_ReturnsLanHttpUrl() {
        var result = ServerInfoEndpoints.GetHspIngestUrl(new HspOptions { PublicIngestUrl = "  " }, new Uri("http://0.0.0.0:5000"), "192.168.1.10");

        Assert.Equal("http://192.168.1.10:5000/api/hsp/ingest", result);
    }

    [Fact]
    public void GetHspIngestUrl_HttpsListener_ReturnsLanHttpsUrl() {
        var result = ServerInfoEndpoints.GetHspIngestUrl(new HspOptions(), new Uri("https://0.0.0.0:5443"), "192.168.1.10");

        Assert.Equal("https://192.168.1.10:5443/api/hsp/ingest", result);
    }

    [Fact]
    public void GetPublicIngestUri_InvalidScheme_Throws() {
        var options = new HspOptions { PublicIngestUrl = "ftp://dash.example/ingest" };

        Assert.Throws<InvalidOperationException>(options.GetPublicIngestUri);
    }

    [Fact]
    public void IsWithinPlayWindow_SampleOutsideWindow_ReturnsFalse() {
        var startedAt = new DateTime(2026, 10, 23, 10, 0, 0, DateTimeKind.Utc);
        var endedAt = startedAt.AddMinutes(3);

        var result = HspEndpoints.IsWithinPlayWindow(endedAt.AddTicks(1), startedAt, endedAt);

        Assert.False(result);
    }
}
