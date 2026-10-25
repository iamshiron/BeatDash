using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Minio;
using Minio.DataModel.Args;
using Npgsql;
using Shiron.BeatDash.API;
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
    public async Task Registration_Disabled_RejectsDirectRequestsAndKeepsSeededAdminLogin() {
        await using var fixture = await Fixture.CreateAsync(disableSignup: true, applyMigrations: true);
        using var client = fixture.CreateClient();
        var usersBefore = await fixture.Db.Users.CountAsync();
        using var registration = await client.PostAsJsonAsync("/api/auth/register", Account.Create().RegisterRequest);
        Assert.Equal(HttpStatusCode.NotFound, registration.StatusCode);
        Assert.Equal(usersBefore, await fixture.Db.Users.CountAsync());

        var status = await client.GetFromJsonAsync<StatusDto>("/api/health");
        Assert.NotNull(status);
        Assert.False(status.SignupEnabled);
        using var login = await client.PostAsJsonAsync("/api/auth/login", new LoginDto {
            Email = "admin@example.test", Password = "Core-test-Password1!"
        });
        login.EnsureSuccessStatusCode();
        Assert.Contains("Admin", (await GetUserAsync(client)).Roles);
    }

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
        using var otherClient = fixture.CreateClient(out var otherCookies);
        var owner = await RegisterAndLoginAsync(ownerClient);
        await RegisterAndLoginAsync(otherClient);
        using var ownerRealtime = await fixture.ConnectRealtimeSocketAsync(ownerClient, ownerCookies);
        using var otherRealtime = await fixture.ConnectRealtimeSocketAsync(otherClient, otherCookies);
        var clientId = Guid.NewGuid();
        var pin = await ownerClient.GetFromJsonAsync<RegisterDeviceResponseDto>("/api/device/register");
        Assert.NotNull(pin);
        TokenPairExpiryDto tokens;
        using (var pair = await ownerClient.PostAsJsonAsync("/api/device/authenticate", new AuthenticateDeviceDto(pin.Pin, clientId))) {
            pair.EnsureSuccessStatusCode();
            tokens = Assert.IsType<TokenPairExpiryDto>(await pair.Content.ReadFromJsonAsync<TokenPairExpiryDto>());
        }
        Assert.Equal("ReceiveDevicePaired", await ReceiveSignalRInvocationAsync(ownerRealtime));
        using (var reusedPin = await ownerClient.PostAsJsonAsync("/api/device/authenticate", new AuthenticateDeviceDto(pin.Pin, Guid.NewGuid())))
            Assert.Equal(HttpStatusCode.Unauthorized, reusedPin.StatusCode);

        const string levelId = "custom_level_core_flow";
        await fixture.AddMapAsync(levelId);
        using var socket = await fixture.ConnectGameSocketAsync(tokens.AccessToken, clientId);
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
        Assert.Equal("ReceiveLiveMapStarted", await ReceiveSignalRInvocationAsync(ownerRealtime, "ReceiveLiveMapStarted"));
        await AssertNoSignalRInvocationAsync(otherRealtime);

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

        using (var ownerSession = await ownerClient.GetAsync($"/api/sessions/{saved.Id}"))
            Assert.Equal(HttpStatusCode.OK, ownerSession.StatusCode);
        using (var otherSession = await otherClient.GetAsync($"/api/sessions/{saved.Id}"))
            Assert.Equal(HttpStatusCode.NotFound, otherSession.StatusCode);

        using var sessions = await otherClient.GetAsync("/api/sessions");
        sessions.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await sessions.Content.ReadAsStringAsync());
        Assert.Equal(0, document.RootElement.GetProperty("totalCount").GetInt32());
    }

    [CoreFlowIntegrationFact]
    public async Task PublicMapCover_IsDeliveredAnonymouslyWhileMapDataRemainsProtected() {
        await using var fixture = await Fixture.CreateAsync();
        using var client = fixture.CreateClient();
        var cover = RandomNumberGenerator.GetBytes(128);
        const string key = "covers/core-flow.png";
        await fixture.UploadAssetAsync(key, cover);
        var mapId = await fixture.AddMapAsync("custom_level_public_cover", key);

        using (var coverResponse = await client.GetAsync($"/api/maps/{mapId}/cover")) {
            coverResponse.EnsureSuccessStatusCode();
            Assert.Equal("image/png", coverResponse.Content.Headers.ContentType!.MediaType);
            Assert.Equal(cover, await coverResponse.Content.ReadAsByteArrayAsync());
        }
        using (var missingCover = await client.GetAsync($"/api/maps/{Guid.NewGuid()}/cover"))
            Assert.Equal(HttpStatusCode.NotFound, missingCover.StatusCode);
        using (var metadata = await client.GetAsync($"/api/maps/{mapId}"))
            Assert.Equal(HttpStatusCode.Found, metadata.StatusCode);
        using (var song = await client.GetAsync($"/api/maps/{mapId}/song"))
            Assert.Equal(HttpStatusCode.Found, song.StatusCode);
        using var lists = await client.GetAsync("/api/lists");
        Assert.Equal(HttpStatusCode.Found, lists.StatusCode);
    }

    [CoreFlowIntegrationFact]
    public async Task MapCatalog_LargePageAndDurationFilters_DoNotOverflow() {
        await using var fixture = await Fixture.CreateAsync();
        using var client = fixture.CreateClient();
        await RegisterAndLoginAsync(client);
        await fixture.AddMapAsync("custom_level_catalog_bounds");

        var emptyPage = await client.GetFromJsonAsync<PagedResult<MapListItemDto>>(
            "/api/maps?Page=2147483647&PageSize=24");
        Assert.NotNull(emptyPage);
        Assert.Equal(1, emptyPage.TotalCount);
        Assert.Empty(emptyPage.Items);

        var minimum = await client.GetFromJsonAsync<PagedResult<MapListItemDto>>(
            "/api/maps?MinDurationSeconds=2147483647");
        Assert.NotNull(minimum);
        Assert.Empty(minimum.Items);

        var maximum = await client.GetFromJsonAsync<PagedResult<MapListItemDto>>(
            "/api/maps?MaxDurationSeconds=2147483647");
        Assert.NotNull(maximum);
        Assert.Single(maximum.Items);
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

    private static async Task<string> ReceiveTextAsync(ClientWebSocket socket, CancellationToken ct = default) {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var payload = new MemoryStream();
        var buffer = new byte[4096];
        WebSocketReceiveResult result;
        do {
            result = await socket.ReceiveAsync(buffer, timeout.Token);
            Assert.Equal(WebSocketMessageType.Text, result.MessageType);
            payload.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return Encoding.UTF8.GetString(payload.GetBuffer(), 0, (int) payload.Length);
    }

    private static async Task<string> ReceiveSignalRInvocationAsync(ClientWebSocket socket, string? expectedTarget = null) {
        var message = await ReceiveTextAsync(socket);
        foreach (var frame in message.Split('\u001e', StringSplitOptions.RemoveEmptyEntries)) {
            using var document = JsonDocument.Parse(frame);
            if (document.RootElement.TryGetProperty("target", out var target)
                && (expectedTarget is null || target.GetString() == expectedTarget))
                return target.GetString()!;
        }
        return await ReceiveSignalRInvocationAsync(socket, expectedTarget);
    }

    private static async Task AssertNoSignalRInvocationAsync(ClientWebSocket socket) {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await ReceiveTextAsync(socket, timeout.Token));
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
        private readonly string _adminConnection = Environment.GetEnvironmentVariable("BEATDASH_TEST_POSTGRES")!;
        private readonly string _databaseName = "core_flow_test_" + Guid.NewGuid().ToString("N");
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "beatdash-core-flow-tests", Guid.NewGuid().ToString("N"));
        private readonly int _udpPort = GetAvailableUdpPort();
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

        public static async Task<Fixture> CreateAsync(bool disableSignup = false, bool applyMigrations = false) {
            var fixture = new Fixture();
            await using var admin = new NpgsqlConnection(fixture._adminConnection);
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{fixture._databaseName}\"", admin);
            await command.ExecuteNonQueryAsync();
            if (!applyMigrations) await fixture.Db.Database.EnsureCreatedAsync();
            await fixture.StartApiAsync(disableSignup, applyMigrations);
            return fixture;
        }

        public HttpClient CreateClient() => CreateClient(out _);

        public HttpClient CreateClient(out CookieContainer cookies) {
            cookies = new CookieContainer();
            return new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = cookies }) { BaseAddress = Address };
        }

        public async Task<Guid> AddMapAsync(string levelId, string? coverImageKey = null) {
            var map = new Beatmap {
                LevelId = levelId, SongName = "Core flow", SongAuthor = "Tests", Mapper = "Tests", Bpm = 120, DurationMs = 120_000,
                CoverImageKey = coverImageKey
            };
            map.Difficulties.Add(new BeatmapDifficulty {
                DifficultyRank = BeatmapDifficultyRank.Expert, DifficultyName = "Expert", NotesPerSecond = 4.2f,
                CuttableObjectCount = 500, BombCount = 2, ObstacleCount = 1, LaneCount = 4,
                CharacteristicSerializedName = "Standard", CharacteristicColorCount = 2,
                CharacteristicRequires360Movement = false, CharacteristicContainsRotationEvents = false
            });
            Db.Beatmaps.Add(map);
            await Db.SaveChangesAsync();
            return map.Id;
        }

        public async Task UploadAssetAsync(string key, byte[] content) {
            if (!await _minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(StorageOptions.BucketAssets)))
                await _minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(StorageOptions.BucketAssets));
            await using var stream = new MemoryStream(content);
            await _minio.PutObjectAsync(new PutObjectArgs()
                .WithBucket(StorageOptions.BucketAssets)
                .WithObject(key)
                .WithStreamData(stream)
                .WithObjectSize(content.Length)
                .WithContentType("image/png"));
        }

        public async Task<ClientWebSocket> ConnectGameSocketAsync(string accessToken, Guid clientId) {
            var socket = new ClientWebSocket();
            socket.Options.SetRequestHeader("Authorization", "Bearer " + accessToken);
            socket.Options.SetRequestHeader("X-Client-Id", clientId.ToString());
            var builder = new UriBuilder(Address) { Scheme = "ws", Path = "/api/client/game" };
            await socket.ConnectAsync(builder.Uri, CancellationToken.None);
            return socket;
        }

        public async Task<ClientWebSocket> ConnectRealtimeSocketAsync(HttpClient client, CookieContainer cookies) {
            using var negotiate = await client.PostAsync("/api/client/web/negotiate?negotiateVersion=1", null);
            negotiate.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await negotiate.Content.ReadAsStringAsync());
            var token = document.RootElement.GetProperty("connectionToken").GetString();
            Assert.NotNull(token);
            var socket = new ClientWebSocket();
            socket.Options.Cookies = cookies;
            var builder = new UriBuilder(Address) { Scheme = "ws", Path = "/api/client/web", Query = "id=" + Uri.EscapeDataString(token) };
            await socket.ConnectAsync(builder.Uri, CancellationToken.None);
            await socket.SendAsync(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e"), WebSocketMessageType.Text, true, CancellationToken.None);
            await ReceiveTextAsync(socket);
            return socket;
        }

        private static int GetAvailableUdpPort() {
            using var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            return ((IPEndPoint) socket.Client.LocalEndPoint!).Port;
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

        private async Task StartApiAsync(bool disableSignup, bool applyMigrations) {
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
            start.Environment["Registration__DisableSignup"] = disableSignup.ToString();
            start.Environment["Database__ApplyMigrationsOnStartup"] = applyMigrations.ToString();
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
