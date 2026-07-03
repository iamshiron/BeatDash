using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Shiron.BeatDash.Data;
using Shiron.BeatDash.Data.Socket;
using Zenject;

namespace Shiron.BeatDash.Mod.Network;

public class NetworkManager : IDisposable {
    private PluginConfig Config { get; set; }
    private readonly HttpClient _httpClient;

    private ClientWebSocket? _socket = null;
    private CancellationTokenSource? _cancellationTokenSource = null;

    private static readonly HttpClient RefreshClient = new();


    [Inject]
    public NetworkManager(PluginConfig config, HttpClient httpClient) {
        Config = config;
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("X-Client-Id", Config.ClientId);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Config.AccessToken);
    }

    public async Task ConnectToSocketAsync() {
        if (_socket is { State: WebSocketState.Open }) {
            return;
        }

        if (await TryConnectAsync()) return;

        Plugin.Log.Debug("WebSocket connection failed, attempting token refresh...");
        if (await TryRefreshTokenAsync()) {
            if (await TryConnectAsync()) return;
            Plugin.Log.Error("Failed to connect to BeatDash socket after token refresh.");
        } else {
            Plugin.Log.Error("Token refresh failed. Please re-pair the device in settings.");
        }
    }

    private async Task<bool> TryConnectAsync() {
        _socket?.Dispose();
        _cancellationTokenSource?.Dispose();

        try {
            _socket = new ClientWebSocket();
            _socket.Options.SetRequestHeader("X-Client-Id", Config.ClientId);
            _socket.Options.SetRequestHeader("Authorization", $"Bearer {Config.AccessToken}");

            _cancellationTokenSource = new CancellationTokenSource();

            await _socket.ConnectAsync(new Uri($"{(Config.UseSsl ? "wss" : "ws")}://{Config.Host}/api/client/game"), _cancellationTokenSource.Token);
            _ = Task.Run(ReceiveLoopAsync, _cancellationTokenSource.Token);
            return true;
        } catch (Exception e) {
            Plugin.Log.Debug($"WebSocket connection attempt failed: {e.Message}");
            _socket?.Dispose();
            _socket = null;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            return false;
        }
    }

    private async Task<bool> TryRefreshTokenAsync() {
        if (string.IsNullOrEmpty(Config.AccessToken) || string.IsNullOrEmpty(Config.RefreshToken)) {
            return false;
        }

        try {
            var content = new StringContent(
                JsonConvert.SerializeObject(new RefreshTokenRequestDto {
                    AccessToken = Config.AccessToken!,
                    RefreshToken = Config.RefreshToken!,
                    ClientId = Guid.Parse(Config.ClientId)
                }),
                Encoding.UTF8,
                "application/json"
            );

            var response = await RefreshClient.PostAsync(
                $"{(Config.UseSsl ? "https" : "http")}://{Config.Host}/api/auth/refresh-token",
                content
            );

            if (!response.IsSuccessStatusCode) {
                Plugin.Log.Debug($"Token refresh rejected: {response.StatusCode}");
                return false;
            }

            var pair = JsonConvert.DeserializeObject<TokenPairDto>(
                await response.Content.ReadAsStringAsync()
            );
            if (pair == null) return false;

            Config.AccessToken = pair.AccessToken;
            Config.RefreshToken = pair.RefreshToken;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Config.AccessToken);

            Plugin.Log.Debug("Access token refreshed.");
            return true;
        } catch (Exception e) {
            Plugin.Log.Debug($"Token refresh failed: {e.Message}");
            return false;
        }
    }

    private async Task ReceiveLoopAsync() {
        var buffer = new byte[4096];
        while (_socket?.State == WebSocketState.Open && _cancellationTokenSource != null && _cancellationTokenSource?.IsCancellationRequested != true) {
            var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource!.Token);
            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            Plugin.Log.Info($"Received: {message}");
        }
    }

    public async Task PostMessageAsync(string message) {
        if (_socket == null || _socket.State != WebSocketState.Open || _cancellationTokenSource == null) {
            return;
        }

        var buffer = Encoding.UTF8.GetBytes(message);
        await _socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, _cancellationTokenSource.Token);
    }

    public async Task PostMessageAsync(BinaryPacket packet) {
        if (_socket == null || _socket.State != WebSocketState.Open || _cancellationTokenSource == null) {
            return;
        }

        await _socket.SendAsync(new ArraySegment<byte>(packet.Payload), WebSocketMessageType.Binary, true, _cancellationTokenSource.Token);
    }

    public void Dispose() {
        _httpClient.Dispose();
        _socket?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}
