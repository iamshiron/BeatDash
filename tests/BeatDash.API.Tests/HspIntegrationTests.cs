using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shiron.BeatDash.API.Endpoints;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;
using Xunit;

namespace Shiron.BeatDash.API.Tests;

[CollectionDefinition("Hsp integration", DisableParallelization = true)]
public sealed class HspIntegrationCollection { }

[Collection("Hsp integration")]
public sealed class HspIntegrationTests {
    [HspIntegrationFact]
    public async Task Ingest_ActiveAndBoundedSessions_EnforcesTokenTrackingAndDedupe() {
        await using var fixture = await Fixture.CreateAsync();
        var now = DateTime.UtcNow;
        var activeStart = now.AddMinutes(-2);
        var orphanStart = now.AddMinutes(-30);
        var autoStart = now.AddMinutes(-10);
        var token = "hsp-test-token";
        var (user, client) = await fixture.SeedAsync(token, activeStart, orphanStart, autoStart);
        using var http = await fixture.StartApiAsync();
        await fixture.Db.Users.Where(x => x.Id == user.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.HealthTrackingEnabled, true));

        var activeSample = Sample(activeStart.AddMinutes(1));
        var accepted = await PushAsync(http, token, activeSample);
        var acceptedResult = await ReadResultAsync(accepted);
        Assert.True(acceptedResult.Accepted == 1, $"accepted={acceptedResult.Accepted}, outOfSession={acceptedResult.OutOfSession}, received={acceptedResult.Received}");

        var duplicate = await PushAsync(http, token, activeSample);
        Assert.Equal(0, (await ReadResultAsync(duplicate)).Accepted);

        var auto = await PushAsync(http, token, Sample(autoStart.AddMinutes(1)));
        Assert.Equal(1, (await ReadResultAsync(auto)).OutOfSession);

        var boundary = await PushAsync(http, token, Sample(orphanStart.AddMinutes(20)));
        Assert.Equal(1, (await ReadResultAsync(boundary)).Accepted);

        var afterBoundary = await PushAsync(http, token, Sample(orphanStart.AddMinutes(20).AddTicks(10)));
        Assert.Equal(1, (await ReadResultAsync(afterBoundary)).OutOfSession);

        user.HealthTrackingEnabled = false;
        await fixture.Db.SaveChangesAsync();
        var disabled = await PushAsync(http, token, Sample(activeStart.AddMinutes(1).AddSeconds(1)));
        Assert.Equal(0, (await ReadResultAsync(disabled)).Accepted);

        fixture.Db.HealthProxyClients.Remove(client);
        await fixture.Db.SaveChangesAsync();
        var revoked = await PushAsync(http, token, Sample(activeStart.AddMinutes(1).AddSeconds(2)));
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);

        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(2, await fixture.Db.SensorSamples.CountAsync());
    }

    private static HspSampleDto Sample(DateTime recordedAt) => new("heart_rate", 120, "bpm", recordedAt, "test");

    private static Task<HttpResponseMessage> PushAsync(HttpClient http, string token, HspSampleDto sample) {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/hsp/ingest") {
            Content = JsonContent.Create(new HspIngestDto("integration-test", [sample]))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http.SendAsync(request);
    }

    private static async Task<HspIngestResultDto> ReadResultAsync(HttpResponseMessage response) {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {(int) response.StatusCode}: {body}");
        return System.Text.Json.JsonSerializer.Deserialize<HspIngestResultDto>(body, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("HSP ingest response was empty.");
    }

    private sealed class HspIntegrationFactAttribute : FactAttribute {
        public HspIntegrationFactAttribute() {
            if (Environment.GetEnvironmentVariable("BEATDASH_TEST_POSTGRES") is null)
                Skip = "Set BEATDASH_TEST_POSTGRES to disposable PostgreSQL infrastructure.";
        }
    }

    private sealed class Fixture : IAsyncDisposable {
        private const string JwtSecret = "hsp-integration-test-secret-at-least-32-characters";
        private readonly string _adminConnection = Environment.GetEnvironmentVariable("BEATDASH_TEST_POSTGRES")!;
        private readonly string _databaseName = "hsp_test_" + Guid.NewGuid().ToString("N");
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "beatdash-hsp-tests", Guid.NewGuid().ToString("N"));
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
            await fixture.Db.Database.MigrateAsync();
            return fixture;
        }

        public async Task<(User User, HealthProxyClient Client)> SeedAsync(string token, DateTime activeStart, DateTime orphanStart, DateTime autoStart) {
            var user = new User { DisplayName = "HSP test", HealthTrackingEnabled = true };
            var map = new Beatmap {
                LevelId = "hsp-test-" + Guid.NewGuid().ToString("N"), SongName = "Test", SongAuthor = "Test", Mapper = "Test", Bpm = 120, DurationMs = 120_000
            };
            var difficulty = new BeatmapDifficulty {
                Beatmap = map, DifficultyRank = BeatmapDifficultyRank.Normal, DifficultyName = "Normal", NotesPerSecond = 1,
                CuttableObjectCount = 1, BombCount = 0, ObstacleCount = 0, LaneCount = 4, CharacteristicSerializedName = "Standard",
                CharacteristicColorCount = 2, CharacteristicRequires360Movement = false, CharacteristicContainsRotationEvents = false
            };
            var client = new HealthProxyClient {
                User = user, UserId = user.Id, Name = "HSP test", TokenHash = HealthIngestToken.Hash(token)
            };
            Db.AddRange(user, difficulty, client);
            Db.PlaySessions.AddRange(
                new PlaySession { User = user, BeatmapDifficulty = difficulty, CorrelationId = 1, StartedAt = activeStart, EndedAt = null, AutoMode = false },
                new PlaySession { User = user, BeatmapDifficulty = difficulty, CorrelationId = 2, StartedAt = orphanStart, EndedAt = null, AutoMode = false },
                new PlaySession { User = user, BeatmapDifficulty = difficulty, CorrelationId = 3, StartedAt = autoStart, EndedAt = null, AutoMode = true });
            await Db.SaveChangesAsync();
            return (user, client);
        }

        public async Task<HttpClient> StartApiAsync() {
            Directory.CreateDirectory(_directory);
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = _directory, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(typeof(HspEndpoints).Assembly.Location);
            start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
            start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
            start.Environment["ConnectionStrings__Default"] = Db.Database.GetConnectionString();
            start.Environment["Jwt__SecretKey"] = JwtSecret;
            start.Environment["Jwt__Issuer"] = "hsp-test";
            start.Environment["Jwt__Audience"] = "hsp-test";
            start.Environment["AdminUser__Email"] = "admin@example.test";
            start.Environment["AdminUser__Password"] = "Hsp-test-Password1!";
            start.Environment["AdminUser__Role"] = "Admin";
            start.Environment["UdpSocket__Host"] = "127.0.0.1";
            start.Environment["UdpSocket__Port"] = GetAvailableUdpPort().ToString();
            start.Environment["BeatSaver__FetchOnStartup"] = "false";
            start.Environment["BeatSaver__ScheduledFetchEnabled"] = "false";
            var ready = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
            var errors = new System.Text.StringBuilder();
            _server = new Process { StartInfo = start, EnableRaisingEvents = true };
            _server.OutputDataReceived += (_, args) => {
                const string prefix = "Now listening on: ";
                lock (errors) errors.AppendLine(args.Data);
                if (args.Data is { } line && line.IndexOf(prefix, StringComparison.Ordinal) is var index && index >= 0)
                    ready.TrySetResult(new Uri(line[(index + prefix.Length)..].Trim()));
            };
            _server.ErrorDataReceived += (_, args) => { lock (errors) errors.AppendLine(args.Data); };
            _server.Exited += (_, _) => { lock (errors) ready.TrySetException(new InvalidOperationException(errors.ToString())); };
            _server.Start();
            _server.BeginOutputReadLine();
            _server.BeginErrorReadLine();
            return new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) {
                BaseAddress = await ready.Task.WaitAsync(TimeSpan.FromSeconds(30))
            };
        }

        private static int GetAvailableUdpPort() {
            using var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            return ((IPEndPoint) socket.Client.LocalEndPoint!).Port;
        }

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
