using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.API.Endpoints;
using Shiron.BeatDash.API.Hosting;
using Shiron.BeatDash.API.Services;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;
using Xunit;

namespace Shiron.BeatDash.API.Tests;

public sealed class RegistrationOptionsTests {
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MapIdentityEndpoints_DisableSignup_RemovesRegistrationRoute(bool disableSignup) {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.Configure<RegistrationOptions>(options => options.DisableSignup = disableSignup);
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<UserManager<User>>(_ => throw new InvalidOperationException("User creation must not be invoked."));
        builder.Services.AddScoped<SignInManager<User>>(_ => throw new InvalidOperationException());
        builder.Services.AddScoped<BeatDashDbContext>(_ => throw new InvalidOperationException());
        builder.Services.AddScoped<ITokenService>(_ => throw new InvalidOperationException());
        builder.Services.AddScoped<IStorageService>(_ => throw new InvalidOperationException());
        await using var app = builder.Build();
        app.UseWebClient();
        app.MapGroup("/api").MapIdentityEndpoints();
        app.MapGroup("/api").MapStatusEndpoints();
        await app.StartAsync();

        var routes = ((IEndpointRouteBuilder) app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().ToArray();
        Assert.Equal(!disableSignup, routes.Any(route => route.RoutePattern.RawText == "/api/auth/register"));
        Assert.Contains(routes, route => route.RoutePattern.RawText == "/api/auth/login");

        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
        using var client = new HttpClient { BaseAddress = new Uri(Assert.Single(addresses.Addresses)) };
        var status = await client.GetStringAsync("/api/health");
        Assert.Contains($"\"signupEnabled\":{(!disableSignup).ToString().ToLowerInvariant()}", status);
        if (!disableSignup) return;

        foreach (var path in new[] { "/api/auth/register", "/api/auth/register/", "/API/AUTH/REGISTER" }) {
            foreach (var body in new[] { "{", "", "{\"displayName\":\"Test\",\"userName\":\"test\",\"email\":\"test@example.com\",\"password\":\"Password1!\"}" }) {
                using var content = new StringContent(body, Encoding.UTF8, "application/json");
                using var response = await client.PostAsync(path, content);
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            }
        }
    }
}
