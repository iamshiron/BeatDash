using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shiron.BeatDash.API.Endpoints;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;
using Xunit;

namespace Shiron.BeatDash.API.Tests;

public sealed class AdminOverviewIntegrationTests {
    [AdminOverviewIntegrationFact]
    public async Task Overview_AnonymousNonAdminAndAdmin_EnforcesAuthorizationAndReturnsAggregates() {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedOperationalDataAsync();
        var address = await fixture.StartApiAsync();

        using var anonymous = Fixture.CreateClient(address);
        using (var response = await anonymous.GetAsync("/api/admin/metrics/overview"))
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        using var ordinary = Fixture.CreateClient(address);
        await LoginAsync(ordinary, Fixture.OrdinaryEmail, Fixture.Password);
        using (var response = await ordinary.GetAsync("/api/admin/metrics/overview"))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var administrator = Fixture.CreateClient(address);
        await LoginAsync(administrator, Fixture.AdminEmail, Fixture.Password);
        using var overviewResponse = await administrator.GetAsync("/api/admin/metrics/overview");
        overviewResponse.EnsureSuccessStatusCode();
        var overview = await overviewResponse.Content.ReadFromJsonAsync<AdminOverviewResponse>();

        Assert.NotNull(overview);
        Assert.Equal(2, overview.Users);
        Assert.Equal(1, overview.Devices);
        Assert.Equal(1, overview.Maps);
        Assert.Equal(1, overview.Plays);
        Assert.Equal(1, overview.AnalysisPipeline.Difficulties);
        Assert.Equal(1, overview.AnalysisPipeline.Analyzed);
        Assert.Equal(1, overview.AnalysisPipeline.AnalysisSucceeded);
        Assert.Equal(1, overview.AnalysisPipeline.FeaturesExtracted);
        Assert.Equal(1, overview.AnalysisPipeline.MetricsScored);
    }

    private static async Task LoginAsync(HttpClient client, string email, string password) {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginDto {
            Email = email,
            Password = password,
            RememberMe = false
        });
        response.EnsureSuccessStatusCode();
    }

    private sealed class AdminOverviewIntegrationFactAttribute : FactAttribute {
        public AdminOverviewIntegrationFactAttribute() {
            if (Environment.GetEnvironmentVariable("BEATDASH_TEST_POSTGRES") is null
                || Environment.GetEnvironmentVariable("BEATDASH_TEST_RUSTFS") is null)
                Skip = "Set BEATDASH_TEST_POSTGRES and BEATDASH_TEST_RUSTFS to disposable test infrastructure.";
        }
    }

    private sealed class Fixture : IAsyncDisposable {
        public const string AdminEmail = "admin@example.test";
        public const string OrdinaryEmail = "ordinary@example.test";
        public const string Password = "Admin-overview-test-Password1!";
        private const string JwtSecret = "admin-overview-integration-test-secret-at-least-32-characters";
        private readonly string _adminConnection = Environment.GetEnvironmentVariable("BEATDASH_TEST_POSTGRES")!;
        private readonly string _databaseName = "admin_overview_test_" + Guid.NewGuid().ToString("N");
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "beatdash-admin-overview-tests", Guid.NewGuid().ToString("N"));
        private Process? _server;

        public BeatDashDbContext Db { get; }

        private Fixture() {
            var connection = new NpgsqlConnectionStringBuilder(_adminConnection) { Database = _databaseName };
            Db = new BeatDashDbContext(new DbContextOptionsBuilder<BeatDashDbContext>().UseNpgsql(connection.ConnectionString).Options);
        }

        public static async Task<Fixture> CreateAsync() {
            var fixture = new Fixture();
            await using var admin = new NpgsqlConnection(fixture._adminConnection);
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{fixture._databaseName}\"", admin);
            await command.ExecuteNonQueryAsync();
            await fixture.Db.Database.EnsureCreatedAsync();
            return fixture;
        }

        public async Task SeedOperationalDataAsync() {
            var ordinary = new User {
                DisplayName = "Ordinary",
                UserName = "ordinary",
                NormalizedUserName = "ORDINARY",
                Email = OrdinaryEmail,
                NormalizedEmail = OrdinaryEmail.ToUpperInvariant(),
                SecurityStamp = Guid.NewGuid().ToString()
            };
            ordinary.PasswordHash = new PasswordHasher<User>().HashPassword(ordinary, Password);
            var difficulty = new BeatmapDifficulty {
                DifficultyRank = BeatmapDifficultyRank.Expert,
                DifficultyName = "Expert",
                NotesPerSecond = 4,
                CuttableObjectCount = 100,
                BombCount = 0,
                ObstacleCount = 0,
                LaneCount = 4,
                CharacteristicSerializedName = "Standard",
                CharacteristicColorCount = 2,
                CharacteristicRequires360Movement = false,
                CharacteristicContainsRotationEvents = false,
                Analysis = new BeatmapDifficultyAnalysis {
                    Status = BeatmapAnalysisStatus.Success,
                    FeatureStatus = FeatureExtractionStatus.Success,
                    MetricStatus = MetricStatus.Success,
                    AnalyzerVersion = 1
                }
            };
            var map = new Beatmap {
                LevelId = "custom_level_admin-overview",
                SongName = "Overview",
                SongAuthor = "BeatDash",
                Mapper = "BeatDash",
                Bpm = 120,
                DurationMs = 120_000,
                Difficulties = [difficulty]
            };

            Db.AddRange(ordinary, map);
            await Db.SaveChangesAsync();
            Db.Add(new Device { ClientId = Guid.CreateVersion7(), Name = "Test device", UserId = ordinary.Id });
            Db.Add(new PlaySession {
                CorrelationId = 1,
                UserId = ordinary.Id,
                BeatmapDifficultyId = difficulty.Id,
                StartedAt = DateTime.UtcNow,
                EndedAt = DateTime.UtcNow
            });
            await Db.SaveChangesAsync();
        }

        public async Task<Uri> StartApiAsync() {
            Directory.CreateDirectory(_directory);
            var start = new ProcessStartInfo("dotnet") {
                WorkingDirectory = _directory,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.ArgumentList.Add(typeof(AdminOverviewResponse).Assembly.Location);
            start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
            start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
            start.Environment["ConnectionStrings__Default"] = Db.Database.GetConnectionString();
            start.Environment["Jwt__SecretKey"] = JwtSecret;
            start.Environment["Jwt__Issuer"] = "admin-overview-test";
            start.Environment["Jwt__Audience"] = "admin-overview-test";
            start.Environment["AdminUser__Email"] = AdminEmail;
            start.Environment["AdminUser__Password"] = Password;
            start.Environment["AdminUser__Role"] = "Admin";
            start.Environment["Backup__MaintenanceMode"] = "false";
            start.Environment["Backup__WorkDirectory"] = Path.Combine(_directory, "server-work");
            start.Environment["BeatSaver__FetchOnStartup"] = "false";
            start.Environment["BeatSaver__ScheduledFetchEnabled"] = "false";
            start.Environment["UdpSocket__Host"] = "127.0.0.1";
            start.Environment["UdpSocket__Port"] = GetAvailableUdpPort().ToString();
            start.Environment["RUSTFS_ENDPOINT"] = Environment.GetEnvironmentVariable("BEATDASH_TEST_RUSTFS");
            start.Environment["RUSTFS_ACCESS_KEY"] = "backup-test";
            start.Environment["RUSTFS_SECRET_KEY"] = "backup-test-secret";
            var ready = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
            _server = new Process { StartInfo = start, EnableRaisingEvents = true };
            var errors = new System.Text.StringBuilder();
            _server.OutputDataReceived += (_, args) => {
                const string prefix = "Now listening on: ";
                if (args.Data is { } line && line.IndexOf(prefix, StringComparison.Ordinal) is var index && index >= 0)
                    ready.TrySetResult(new Uri(line[(index + prefix.Length)..].Trim()));
            };
            _server.ErrorDataReceived += (_, args) => { lock (errors) errors.AppendLine(args.Data); };
            _server.Exited += (_, _) => { lock (errors) ready.TrySetException(new InvalidOperationException(errors.ToString())); };
            _server.Start();
            _server.BeginOutputReadLine();
            _server.BeginErrorReadLine();
            return await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }

        private static int GetAvailableUdpPort() {
            using var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            return ((IPEndPoint) socket.Client.LocalEndPoint!).Port;
        }

        public static HttpClient CreateClient(Uri address) => new(new HttpClientHandler {
            AllowAutoRedirect = false,
            CookieContainer = new System.Net.CookieContainer()
        }) { BaseAddress = address };

        public async ValueTask DisposeAsync() {
            if (_server is not null) {
                if (!_server.HasExited) _server.Kill(entireProcessTree: true);
                await _server.WaitForExitAsync();
                _server.Dispose();
            }
            await Db.DisposeAsync();
            await using var admin = new NpgsqlConnection(_adminConnection);
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE \"{_databaseName}\" WITH (FORCE)", admin);
            await command.ExecuteNonQueryAsync();
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }
    }
}
