using System.Net;
using System.Net.Http.Json;
using Shiron.BeatDash.API.Endpoints;
using Shiron.BeatDash.Data;
using Xunit;

namespace Shiron.BeatDash.API.Tests;

public sealed class DesktopModeIntegrationTests {
    private const string SessionToken = "desktop-integration-session-token-0123456789";

    [Fact]
    public async Task AccountAndBackupRoutes_DesktopMode_AreNotMapped() {
        await using var fixture = await Fixture.CreateAsync();
        using var client = fixture.Api.CreateClient();

        var status = await client.GetFromJsonAsync<StatusDto>("/api/health");
        Assert.NotNull(status);
        Assert.True(status.Desktop);
        Assert.False(status.SignupEnabled);
        foreach (var route in new[] { "/api/auth/register", "/api/auth/login", "/api/auth/logout", "/api/auth/change-password" }) {
            using var response = await client.PostAsJsonAsync(route, new { });
            Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{route} returned {(int) response.StatusCode}");
        }
        // Backup administration needs credentials; outside maintenance mode (which desktop mode refuses) it is unavailable.
        using var backup = await client.PostAsJsonAsync("/api/admin/backups/login", new { });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, backup.StatusCode);
    }

    [Fact]
    public async Task Session_MissingOrWrongToken_DoesNotSignIn() {
        await using var fixture = await Fixture.CreateAsync();
        using var client = fixture.Api.CreateClient();

        using (var missing = await client.GetAsync("/api/desktop/session"))
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using (var wrong = await client.GetAsync("/api/desktop/session?token=" + SessionToken[..^1] + "x"))
            Assert.Equal(HttpStatusCode.NotFound, wrong.StatusCode);
        using var me = await client.GetAsync("/api/auth/me");
        Assert.False(me.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Session_ValidToken_SignsInLocalAdministratorWhoCanPairTheMod() {
        await using var fixture = await Fixture.CreateAsync();
        using var client = fixture.Api.CreateClient();

        using (var session = await client.GetAsync("/api/desktop/session?token=" + SessionToken)) {
            Assert.Equal(HttpStatusCode.Redirect, session.StatusCode);
            Assert.Equal("/", session.Headers.Location!.OriginalString);
        }
        var user = await client.GetFromJsonAsync<UserInfoDto>("/api/auth/me");
        Assert.NotNull(user);
        Assert.Equal("Player", user.DisplayName);
        Assert.Contains("Admin", user.Roles);

        var pin = await client.GetFromJsonAsync<RegisterDeviceResponseDto>("/api/device/register");
        Assert.NotNull(pin);
        var clientId = Guid.NewGuid();
        using var mod = fixture.Api.CreateClient();
        using var pair = await mod.PostAsJsonAsync("/api/device/authenticate", new AuthenticateDeviceDto(pin.Pin, clientId));
        pair.EnsureSuccessStatusCode();
        var tokens = await pair.Content.ReadFromJsonAsync<TokenPairExpiryDto>();
        Assert.NotNull(tokens);
        using var refresh = await mod.PostAsJsonAsync("/api/auth/refresh-token", new RefreshTokenRequestDto {
            AccessToken = tokens.AccessToken, RefreshToken = tokens.RefreshToken, ClientId = clientId
        });
        refresh.EnsureSuccessStatusCode();
    }

    private sealed class Fixture : IAsyncDisposable {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "beatdash-desktop-mode-tests", Guid.NewGuid().ToString("N"));
        private TestInfrastructure _infrastructure = null!;

        public ApiProcess Api { get; private set; } = null!;

        public static async Task<Fixture> CreateAsync() {
            var fixture = new Fixture { _infrastructure = await TestInfrastructure.CreateAsync(TestBackend.SqliteFileSystem) };
            fixture.Api = await ApiProcess.StartAsync(fixture._directory, environment => {
                environment["Database__Provider"] = "Sqlite";
                environment["ConnectionStrings__Default"] = fixture._infrastructure.ConnectionString;
                environment["Database__ApplyMigrationsOnStartup"] = "true";
                environment["Storage__Provider"] = "FileSystem";
                environment["Storage__RootDirectory"] = fixture._infrastructure.StorageOptions.RootDirectory;
                environment["Desktop__Enabled"] = "true";
                environment["Desktop__SessionToken"] = SessionToken;
            });
            return fixture;
        }

        public async ValueTask DisposeAsync() {
            if (Api is not null) await Api.DisposeAsync();
            await _infrastructure.DisposeAsync();
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }
    }
}
