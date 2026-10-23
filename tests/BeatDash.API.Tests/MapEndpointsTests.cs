using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shiron.BeatDash.API.Endpoints;
using Shiron.BeatDash.API.Services;
using Shiron.BeatDash.API.Services.BeatSaver;
using Shiron.BeatDash.DB;
using Xunit;

namespace Shiron.BeatDash.API.Tests;

public sealed class MapEndpointsTests {
    [Fact]
    public async Task MapCover_AnonymousAccess_DoesNotExposeOtherMapEndpoints() {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddSingleton<BeatDashDbContext>();
        builder.Services.AddSingleton<IStorageService, MinioStorageService>();
        builder.Services.AddSingleton<IMapAudioService, MapAudioService>();
        builder.Services.AddSingleton<IBeatmapPersistenceService, BeatmapPersistenceService>();
        builder.Services.AddSingleton<IBeatSaverFetchTrigger, BeatSaverFetchTrigger>();
        await using var app = builder.Build();
        app.MapMapEndpoints();

        var endpoints = ((IEndpointRouteBuilder) app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();
        var cover = Assert.Single(endpoints, endpoint => endpoint.RoutePattern.RawText == "/maps/{mapId:Guid}/cover");
        Assert.NotNull(cover.Metadata.GetMetadata<IAllowAnonymous>());
        Assert.Null(cover.Metadata.GetMetadata<IAuthorizeData>());

        foreach (var endpoint in endpoints.Where(endpoint => endpoint != cover)) {
            Assert.Null(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
            Assert.NotNull(endpoint.Metadata.GetMetadata<IAuthorizeData>());
        }
    }
}
