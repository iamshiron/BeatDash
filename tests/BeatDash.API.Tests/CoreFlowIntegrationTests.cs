using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Minio;
using Minio.DataModel.Args;
using Npgsql;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.API.Endpoints;
using Shiron.BeatDash.Data;
using Shiron.BeatDash.Data.Socket;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;
using Xunit;

namespace Shiron.BeatDash.API.Tests;

public sealed class CoreFlowIntegrationTests {
    [CoreFlowIntegrationFact]
    public async Task AccountCookie_RegisterLoginAndLogout_ControlsAuthenticatedSession() {
        await using var fixture = await Fixture.CreateAsync();
        using var client = fixture.CreateClient();
        var account = Account.Create();

        using (var register = await client.PostAsJsonAsync("/api/auth/register", account.RegisterRequest))
            register.EnsureSuccessStatusCode();
        using (var anonymous = await client.GetAsync("/api/auth/me"))
            Assert.Equal(HttpStatusCode.Found, anonymous.StatusCode);

        using (var login = await client.PostAsJsonAsync("/api/auth/login", account.LoginRequest))
            login.EnsureSuccessStatusCode();
        var user = await GetUserAsync(client);
        Assert.Equal(account.Email, user.Email);
        Assert.Equal(account.DisplayName, user.DisplayName);

        using (var logout = await client.PostAsync("/api/auth/logout", null))
            logout.EnsureSuccessStatusCode();
        using var afterLogout = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Found, afterLogout.StatusCode);
    }

    [CoreFlowIntegrationFact]
    public async Task DevicePinPairing_RefreshRotationAndUserBoundaries_WorkEndToEnd() {
        await using var fixture = await Fixture.CreateAsync();
        using var first = fixture.CreateClient();
        using var second = fixture.CreateClient();
        var owner = await RegisterAndLoginAsync(first);
        await RegisterAndLoginAsync(second);

        var pin = await first.GetFromJsonAsync<RegisterDeviceResponseDto>("/api/device/register");
        Assert.NotNull(pin);
        var clientId = Guid.NewGuid();
        using var pair = await first.PostAsJsonAsync("/api/device/authenticate", new AuthenticateDeviceDto(pin.Pin, clientId));
        pair.EnsureSuccessStatusCode();
        var tokens = await pair.Content.ReadFromJsonAsync<TokenPairExpiryDto>();
        Assert.NotNull(tokens);

        using (var refresh = await first.PostAsJsonAsync("/api/auth/refresh-token", new RefreshTokenRequestDto {
            AccessToken = tokens.AccessToken, RefreshToken = tokens.RefreshToken, ClientId = clientId
        })) {
            refresh.EnsureSuccessStatusCode();
            var rotated = await refresh.Content.ReadFromJsonAsync<TokenPairExpiryDto>();
            Assert.NotNull(rotated);
            Assert.NotEqual(tokens.RefreshToken, rotated.RefreshToken);
        }
        using (var reused = await first.PostAsJsonAsync("/api/auth/refresh-token", new RefreshTokenRequestDto {
            AccessToken = tokens.AccessToken, RefreshToken = tokens.RefreshToken, ClientId = clientId
        }))
            Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);

        using (var devices = await first.GetAsync("/api/device/")) {
            devices.EnsureSuccessStatusCode();
            var owned = await devices.Content.ReadFromJsonAsync<List<DeviceResponseDto>>();
            var device = Assert.Single(owned!);
            Assert.Equal(clientId, device.ClientId);
        }
        using (var otherDevices = await second.GetAsync("/api/device/")) {
            otherDevices.EnsureSuccessStatusCode();
            Assert.Empty(await otherDevices.Content.ReadFromJsonAsync<List<DeviceResponseDto>>() ?? []);
        }
        using (var crossUserDelete = await second.DeleteAsync($"/api/device/{clientId}"))
            Assert.Equal(HttpStatusCode.NoContent, crossUserDelete.StatusCode);
        var retained = await first.GetFromJsonAsync<List<DeviceResponseDto>>("/api/device/");
        Assert.Equal(clientId, Assert.Single(retained!).ClientId);
        Assert.NotEqual(Guid.Empty, owner.ID);
    }

    [CoreFlowIntegrationFact]
    public async Task GameWebSocket_MapPlayIsPersistedOnlyForAuthenticatedOwner() {
        await using var fixture = await Fixture.CreateAsync();
        using var ownerClient = fixture.CreateClient(out var ownerCookies);
        using var otherClient = fixture.CreateClient();
        var owner = await RegisterAndLoginAsync(ownerClient);
        await RegisterAndLoginAsync(otherClient);
        var clientId = Guid.NewGuid();
        var pin = await ownerClient.GetFromJsonAsync<RegisterDeviceResponseDto>("/api/device/register");
        Assert.NotNull(pin);
        using (var pair = await ownerClient.PostAsJsonAsync("/api/device/authenticate", new AuthenticateDeviceDto(pin.Pin, clientId)))
            pair.EnsureSuccessStatusCode();

        const string levelId = "custom_level_core_flow";
        await fixture.AddMapAsync(levelId);
        using var socket = await fixture.ConnectGameSocketAsync(ownerCookies, clientId);
        var handshake = await ReceiveTextAsync(socket);
        Assert.Contains("UdpHandshakeMessage", handshake, StringComparison.Ordinal);

        await SendPacketAsync(socket, BinaryPacketTypes.MapStart, new MapStartMessage {
            CorrelationId = 0, LevelId = levelId, DurationMs = 120_000, NotesPerSecond = 4.2f,
            SongName = "Core flow", SongSubName = "", SongAuthor = "Tests", Mapper = "Tests", Bpm = 120,
            Difficulty = "Expert", DifficultyName = "Expert", NoteJumpSpeed = 18, BombCount = 2,
            ObstacleCount = 1, CuttableObjectCount = 500, LaneCount = 4,
            Characteristic = new BeatmapCharacteristic {
                ColorCount = 2, Requires360Movement = false, ContainsRotationEvents = false,
                SerializedName = "Standard", LocalizationKey = "STANDARD", DescriptionLocalizationKey = "STANDARD_DESC"
            },
            AutoMode = false, ModifierFlags = 0, SongSpeed = 1, NotesPerHandLeft = 250, NotesPerHandRight = 250,
            NpsCurve = [], WallTimeline = [], BombPositions = []
        });
        using var correlationDocument = JsonDocument.Parse(await ReceiveTextAsync(socket));
        var correlationId = correlationDocument.RootElement.GetProperty("CorrelationId").GetInt32();

        await SendPacketAsync(socket, BinaryPacketTypes.MapState, new MapStateMessage {
            CorrelationId = correlationId, LevelId = levelId, State = "Finished",
            Results = new MapResults {
                Score = 123_456, MultipliedScore = 120_000, MaxMultipliedScore = 130_000, Accuracy = .923f,
                Rank = "S", FullCombo = false, MaxCombo = 321, GoodCuts = 450, BadCuts = 3,
                MissedNotes = 2, Energy = .8f, EndSongTime = 119.5f
            }
        });

        var saved = await fixture.WaitForPlayAsync(owner.ID, correlationId);
        Assert.Equal(PlaySessionEndReason.Finished, saved.EndReason);
        Assert.Equal(123_456, saved.Results!.Score);
        Assert.Equal(119_500, saved.Results.EndSongTimeMs);

        using var sessions = await otherClient.GetAsync("/api/sessions");
        sessions.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await sessions.Content.ReadAsStringAsync());
        Assert.Equal(0, document.RootElement.GetProperty("totalCount").GetInt32());
    }

    private static async Task<UserInfoDto> RegisterAndLoginAsync(HttpClient client) {
        var account = Account.Create();
        using (var register = await client.PostAsJsonAsync("/api/auth/register", account.RegisterRequest))
            register.EnsureSuccessStatusCode();
        using (var login = await client.PostAsJsonAsync("/api/auth/login", account.LoginRequest))
            login.EnsureSuccessStatusCode();
        return await GetUserAsync(client);
    }

    private static async Task<UserInfoDto> GetUserAsync(HttpClient client) {
        var user = await client.GetFromJsonAsync<UserInfoDto>("/api/auth/me");
        return Assert.IsType<UserInfoDto>(user);
    }

    private static async Task SendPacketAsync<T>(ClientWebSocket socket, BinaryPacketTypes type, T message) {
        var json = JsonSerializer.SerializeToUtf8Bytes(message);
        var packet = new BinaryPacket(type, json);
        await socket.SendAsync(packet.Payload, WebSocketMessageType.Binary, true, CancellationToken.None);
    }

    private static async Task<string> ReceiveTextAsync(ClientWebSocket socket) {
        var buffer = new ArraySegment<byte>(new byte[4096]);
        var result = await socket.ReceiveAsync(buffer, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(WebSocketMessageType.Text, result.MessageType);
        return Encoding.UTF8.GetString(buffer.Array!, 0, result.Count);
    }

    private sealed record Account(string DisplayName, string UserName, string Email, string Password) {
        public RegisterDto RegisterRequest => new() {
            DisplayName = DisplayName, UserName = UserName, Email = Email, Password = Password
        };

        public LoginDto LoginRequest => new() { Email = Email, Password = Password };

        public static Account Create() {
            var suffix = Guid.NewGuid().ToString("N")[..12];
            return new Account("Core " + suffix, "core" + suffix, $"core-{suffix}@example.test", "Core-test-Password1!");
        }
    }

    private sealed class CoreFlowIntegrationFactAttribute : FactAttribute {
        public CoreFlowIntegrationFactAttribute() {
            if (Environment.GetEnvironmentVariable("BEATDASH_TEST_POSTGRES") is null
                || Environment.GetEnvironmentVariable("BEATDASH_TEST_RUSTFS") is null)
                Skip = "Set BEATDASH_TEST_POSTGRES and BEATDASH_TEST_RUSTFS to disposable test infrastructure.";
        }
    }

    private sealed class Fixture : IAsyncDisposable {
        private const string JwtSecret = "core-flow-integration-test-secret-at-least-32-characters";
        private static int _nextUdpPort = 41000;
        private readonly string _adminConnection = Environment.GetEnvironmentVariable("BEATDASH_TEST_POSTGRES")!;
        private readonly string _databaseName = "core_flow_test_" + Guid.NewGuid().ToString("N");
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "beatdash-core-flow-tests", Guid.NewGuid().ToString("N"));
        private readonly int _udpPort = Interlocked.Increment(ref _nextUdpPort);
        private readonly IMinioClient _minio;
        private Process? _server;

        private Fixture() {
            var connection = new NpgsqlConnectionStringBuilder(_adminConnection) { Database = _databaseName };
            Db = new BeatDashDbContext(new DbContextOptionsBuilder<BeatDashDbContext>().UseNpgsql(connection.ConnectionString).Options);
            StorageOptions = new StorageOptions {
                BucketAssets = "assets-" + Guid.NewGuid().ToString("N"), BucketUserData = "users-" + Guid.NewGuid().ToString("N")
            };
            _minio = new MinioClient().WithEndpoint(Environment.GetEnvironmentVariable("BEATDASH_TEST_RUSTFS"))
                .WithCredentials("backup-test", "backup-test-secret").Build();
        }

        public BeatDashDbContext Db { get; }
        public StorageOptions StorageOptions { get; }
        public Uri Address { get; private set; } = null!;

        public static async Task<Fixture> CreateAsync() {
            var fixture = new Fixture();
            await using var admin = new NpgsqlConnection(fixture._adminConnection);
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{fixture._databaseName}\"", admin);
            await command.ExecuteNonQueryAsync();
            await fixture.Db.Database.EnsureCreatedAsync();
            await fixture.StartApiAsync();
            return fixture;
        }

        public HttpClient CreateClient() => CreateClient(out _);

        public HttpClient CreateClient(out CookieContainer cookies) {
            cookies = new CookieContainer();
            return new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = cookies }) { BaseAddress = Address };
        }

        public async Task AddMapAsync(string levelId) {
            var map = new Beatmap {
                LevelId = levelId, SongName = "Core flow", SongAuthor = "Tests", Mapper = "Tests", Bpm = 120, DurationMs = 120_000
            };
            map.Difficulties.Add(new BeatmapDifficulty {
                DifficultyRank = BeatmapDifficultyRank.Expert, DifficultyName = "Expert", NotesPerSecond = 4.2f,
                CuttableObjectCount = 500, BombCount = 2, ObstacleCount = 1, LaneCount = 4,
                CharacteristicSerializedName = "Standard", CharacteristicColorCount = 2,
                CharacteristicRequires360Movement = false, CharacteristicContainsRotationEvents = false
            });
            Db.Beatmaps.Add(map);
            await Db.SaveChangesAsync();
        }

        public async Task<ClientWebSocket> ConnectGameSocketAsync(CookieContainer cookies, Guid clientId) {
            var socket = new ClientWebSocket();
            socket.Options.Cookies = cookies;
            socket.Options.SetRequestHeader("X-Client-Id", clientId.ToString());
            var builder = new UriBuilder(Address) { Scheme = "ws", Path = "/api/client/game" };
            await socket.ConnectAsync(builder.Uri, CancellationToken.None);
            return socket;
        }

        public async Task<PlaySession> WaitForPlayAsync(Guid userId, int correlationId) {
            var timeout = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < timeout) {
                await using var db = new BeatDashDbContext(new DbContextOptionsBuilder<BeatDashDbContext>()
                    .UseNpgsql(Db.Database.GetConnectionString()).Options);
                var session = await db.PlaySessions.Include(p => p.Results)
                    .SingleOrDefaultAsync(p => p.UserId == userId && p.CorrelationId == correlationId);
                if (session?.EndedAt is not null) return session;
                await Task.Delay(100);
            }
            throw new TimeoutException("The game WebSocket did not persist the completed play.");
        }

        private async Task StartApiAsync() {
            Directory.CreateDirectory(_directory);
            var start = new ProcessStartInfo("dotnet") {
                WorkingDirectory = _directory, RedirectStandardOutput = true, RedirectStandardError = true
            };
            start.ArgumentList.Add(typeof(IdentityEndpoints).Assembly.Location);
            start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
            start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
            start.Environment["ConnectionStrings__Default"] = Db.Database.GetConnectionString();
            start.Environment["Jwt__SecretKey"] = JwtSecret;
            start.Environment["Jwt__Issuer"] = "core-flow-test";
            start.Environment["Jwt__Audience"] = "core-flow-test";
            start.Environment["AdminUser__Email"] = "admin@example.test";
            start.Environment["AdminUser__Password"] = "Core-test-Password1!";
            start.Environment["AdminUser__Role"] = "Admin";
            start.Environment["Backup__MaintenanceMode"] = "false";
            start.Environment["Backup__WorkDirectory"] = Path.Combine(_directory, "server-work");
            start.Environment["BeatSaver__FetchOnStartup"] = "false";
            start.Environment["BeatSaver__ScheduledFetchEnabled"] = "false";
            start.Environment["BeatSaver__FetchOnNewMap"] = "false";
            start.Environment["UdpSocket__Host"] = "127.0.0.1";
            start.Environment["UdpSocket__Port"] = _udpPort.ToString();
            start.Environment["RUSTFS_ENDPOINT"] = Environment.GetEnvironmentVariable("BEATDASH_TEST_RUSTFS");
            start.Environment["RUSTFS_ACCESS_KEY"] = "backup-test";
            start.Environment["RUSTFS_SECRET_KEY"] = "backup-test-secret";
            start.Environment["RUSTFS_BUCKET_ASSETS"] = StorageOptions.BucketAssets;
            start.Environment["RUSTFS_BUCKET_USER_DATA"] = StorageOptions.BucketUserData;
            var ready = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
            _server = new Process { StartInfo = start, EnableRaisingEvents = true };
            var errors = new StringBuilder();
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
            Address = await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
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
            foreach (var bucket in new[] { StorageOptions.BucketAssets, StorageOptions.BucketUserData }) {
                if (!await _minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket))) continue;
                await foreach (var item in _minio.ListObjectsEnumAsync(new ListObjectsArgs().WithBucket(bucket).WithRecursive(true)))
                    await _minio.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(bucket).WithObject(item.Key));
                await _minio.RemoveBucketAsync(new RemoveBucketArgs().WithBucket(bucket));
            }
            _minio.Dispose();
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }
    }
}
