using System.Reflection;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Shiron.BeatDash.API.Endpoints;
using Xunit;

namespace Shiron.BeatDash.API.Tests;

public sealed class StatusEndpointsTests {
    [Fact]
    public async Task MapStatusEndpoints_AnonymousRequest_ReturnsGeneratedVersion() {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        app.MapGroup("/api").MapStatusEndpoints();
        await app.StartAsync();

        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
        using var client = new HttpClient { BaseAddress = new Uri(Assert.Single(addresses.Addresses)) };
        var status = await client.GetFromJsonAsync<StatusDto>("/api/health");

        Assert.NotNull(status);
        Assert.Equal("OK", status.Message);
        Assert.Equal(GitVersionInformation.SemVer, status.Version);
    }

    [Fact]
    public void GetStatus_CurrentBuild_ReturnsGeneratedAssemblyVersion() {
        var status = StatusEndpoints.GetStatus();
        var assembly = typeof(StatusEndpoints).Assembly;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        var fileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()!.Version;

        Assert.Equal("OK", status.Message);
        Assert.Equal(GitVersionInformation.SemVer, status.Version);
        Assert.StartsWith(status.Version, informationalVersion);
        Assert.Equal(GitVersionInformation.InformationalVersion, informationalVersion);
        Assert.Equal(GitVersionInformation.AssemblySemVer, assembly.GetName().Version!.ToString());
        Assert.Equal(GitVersionInformation.AssemblySemFileVer, fileVersion);
    }
}
