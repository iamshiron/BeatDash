using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shiron.BeatDash.Data;
using Shiron.BeatDash.Data.Socket;
using Zenject;

namespace Shiron.BeatDash.Mod.Network;

public class NetworkManager : IDisposable {
    private PluginConfig Config { get; set; }
    private readonly HttpClient _httpClient;

    private ClientWebSocket? _socket = null;
    private CancellationTokenSource? _cancellationTokenSource = null;


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

        _socket = new ClientWebSocket();
        _socket.Options.SetRequestHeader("X-Client-Id", Config.ClientId);
        _socket.Options.SetRequestHeader("Authorization", $"Bearer {Config.AccessToken}");

        _cancellationTokenSource = new CancellationTokenSource();

        await _socket.ConnectAsync(new Uri($"{(Config.UseSsl ? "wss" : "ws")}://{Config.Host}/api/client/ws"), _cancellationTokenSource.Token);
        _ = Task.Run(ReceiveLoopAsync, _cancellationTokenSource.Token);
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
