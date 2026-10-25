using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Shiron.BeatDash.API.Endpoints;
using Shiron.BeatDash.API.Hosting;
using Xunit;

namespace Shiron.BeatDash.API.Tests;

public sealed class WebClientHostingExtensionsTests {
    [Fact]
    public async Task UseWebClient_DashboardRequests_ServesIndexAssetsAndClientRoutes() {
        await using var fixture = await Fixture.CreateAsync();
        var index = await fixture.Client.GetStringAsync("/");
        Assert.Contains("<html", index);
        Assert.Equal(index, await fixture.Client.GetStringAsync("/plays/123?view=details"));
        Assert.Equal(index, await fixture.Client.GetStringAsync("/auth/login"));

        var stylesheet = Regex.Match(index, "href=\"([^\"]+\\.css)\"");
        Assert.True(stylesheet.Success, "The dashboard must reference its bundled stylesheet.");
        using var asset = await fixture.Client.GetAsync(stylesheet.Groups[1].Value);
        Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
        Assert.Equal("text/css", asset.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await asset.Content.ReadAsStringAsync());

        var assetPaths = Regex.Matches(index, "(?:src|href)=\"(/assets/[^\"]+)\"")
            .Select(match => match.Groups[1].Value).Distinct().ToArray();
        Assert.NotEmpty(assetPaths);
        foreach (var path in assetPaths) {
            using var response = await fixture.Client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
            Assert.NotEmpty(await response.Content.ReadAsByteArrayAsync());
        }
    }

    [Fact]
    public async Task UseWebClient_ApiRequests_PreservesEndpointsAndReturns404ForUnknownApiRoutes() {
        await using var fixture = await Fixture.CreateAsync();
        using var status = await fixture.Client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.Equal("application/json", status.Content.Headers.ContentType?.MediaType);

        foreach (var path in new[] { "/api", "/api/missing", "/api/missing.json", "/assets/missing.css" }) {
            using var response = await fixture.Client.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    private sealed class Fixture(WebApplication app, HttpClient client, string? temporaryDirectory) : IAsyncDisposable {
        public HttpClient Client { get; } = client;

        public static async Task<Fixture> CreateAsync() {
            var webRoot = Environment.GetEnvironmentVariable("BEATDASH_TEST_WEB_ROOT");
            string? temporaryDirectory = null;
            if (string.IsNullOrEmpty(webRoot)) {
                temporaryDirectory = Directory.CreateTempSubdirectory("beatdash-web-").FullName;
                webRoot = Path.Combine(temporaryDirectory, "wwwroot");
                Directory.CreateDirectory(Path.Combine(webRoot, "assets"));
                await File.WriteAllTextAsync(Path.Combine(webRoot, "index.html"),
                    "<!doctype html><html><head><link rel=\"stylesheet\" href=\"/assets/app.css\"></head><body>BeatDash</body></html>");
                await File.WriteAllTextAsync(Path.Combine(webRoot, "assets", "app.css"), "body { color: black; }");
            }
            Assert.True(File.Exists(Path.Combine(webRoot, "index.html")), "The bundled dashboard index.html is missing.");

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
                EnvironmentName = "Production",
                WebRootPath = Path.GetFullPath(webRoot)
            });
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            var app = builder.Build();
            app.UseWebClient();
            app.MapGroup("/api").MapStatusEndpoints();
            await app.StartAsync();
            var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
            var client = new HttpClient { BaseAddress = new Uri(Assert.Single(addresses.Addresses)) };
            return new Fixture(app, client, temporaryDirectory);
        }

        public async ValueTask DisposeAsync() {
            Client.Dispose();
            await app.DisposeAsync();
            if (temporaryDirectory is not null) Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
