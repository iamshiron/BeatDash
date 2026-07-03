using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shiron.BeatDash.Data;
using Shiron.BeatDash.Data.Socket;
using Zenject;

namespace Shiron.BeatDash.Mod.Network;

public class NetworkManager : IDisposable {
    private const int HolepunchMaxAttempts = 6;
    private const int HolepunchIntervalMs = 500;

    private PluginConfig Config { get; set; }
    private readonly HttpClient _httpClient;

    private ClientWebSocket? _socket = null;
    private CancellationTokenSource? _cancellationTokenSource = null;

    private UdpClient? _udp = null;
    private volatile bool _isUdpAvailable = false;

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
        ResetUdp();

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
        var messageStream = new MemoryStream();

        while (_socket?.State == WebSocketState.Open && _cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested) {
            messageStream.SetLength(0);

            WebSocketReceiveResult result;
            try {
                result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);
            } catch {
                break;
            }

            if (result.MessageType == WebSocketMessageType.Close) {
                try {
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                } catch { }
                break;
            }

            messageStream.Write(buffer, 0, result.Count);
            while (!result.EndOfMessage && _socket?.State == WebSocketState.Open) {
                result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);
                messageStream.Write(buffer, 0, result.Count);
            }

            if (result.MessageType == WebSocketMessageType.Text) {
                HandleTextMessage(messageStream.ToArray());
            }
        }

        messageStream.Dispose();
    }

    /// <summary>
    /// Dispatches a complete text (JSON) message. The server pushes the UDP
    /// handshake and bound-ack as JSON; everything else is logged.
    /// </summary>
    private void HandleTextMessage(byte[] payload) {
        string json;
        try {
            json = Encoding.UTF8.GetString(payload);
        } catch {
            return;
        }

        string? type;
        try {
            type = JObject.Parse(json).Value<string>("Type");
        } catch (Exception e) {
            Plugin.Log.Debug($"Failed to parse socket JSON: {e.Message}");
            return;
        }

        switch (type) {
            case nameof(UdpHandshakeMessage):
                HandleHandshake(json);
                break;
            case nameof(UdpBoundMessage):
                _isUdpAvailable = true;
                Plugin.Log.Debug("UDP endpoint bound — binary traffic now prefers UDP.");
                break;
            default:
                Plugin.Log.Info($"Received: {json}");
                break;
        }
    }

    private void HandleHandshake(string json) {
        if (Config.DisableUdp) return;
        try {
            var msg = JsonConvert.DeserializeObject<UdpHandshakeMessage>(json);
            if (msg == null) return;
            _ = StartUdpAsync(msg.Ticket, msg.Port);
        } catch (Exception e) {
            Plugin.Log.Debug($"Failed to start UDP handshake: {e.Message}");
        }
    }

    /// <summary>
    /// Opens the UDP socket and sends the holepunch packet (retried until the
    /// server acknowledges over TCP or the attempts run out).
    /// </summary>
    private async Task StartUdpAsync(Guid ticket, int port) {
        ResetUdp();
        try {
            var hostPart = Config.Host.Split(':')[0];
            var address = Dns.GetHostAddresses(hostPart)[0];

            _udp = new UdpClient();
            _udp.Connect(new IPEndPoint(address, port));

            Plugin.Log.Debug($"Starting UDP holepunch to {address}:{port}.");
            var holepunch = UdpPacket.Build(BinaryPacketTypes.Holepunch, ticket.ToByteArray());
            await HolepunchRetryAsync(holepunch);
        } catch (Exception e) {
            Plugin.Log.Debug($"UDP setup failed, staying on TCP: {e.Message}");
            ResetUdp();
        }
    }

    private async Task HolepunchRetryAsync(byte[] holepunch) {
        for (var i = 0; i < HolepunchMaxAttempts; i++) {
            if (_isUdpAvailable || _udp == null) return;
            try {
                await _udp.SendAsync(holepunch, holepunch.Length);
            } catch (Exception e) {
                Plugin.Log.Debug($"UDP holepunch send failed: {e.Message}");
                return;
            }
            await Task.Delay(HolepunchIntervalMs);
        }

        Plugin.Log.Debug("UDP holepunch timed out — staying on TCP.");
        ResetUdp();
    }

    /// <summary>
    /// Sends a JSON text message over the TCP WebSocket.
    /// </summary>
    public async Task PostMessageAsync(string message) {
        if (_socket == null || _socket.State != WebSocketState.Open || _cancellationTokenSource == null) {
            return;
        }

        var buffer = Encoding.UTF8.GetBytes(message);
        await _socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, _cancellationTokenSource.Token);
    }

    /// <summary>
    /// Serializes a message to JSON and sends it as a binary packet, preferring
    /// UDP when available. Set <paramref name="forceTcp"/> for payloads that need
    /// reliable delivery (e.g. map-start metadata, state changes).
    /// </summary>
    public async Task PostJsonBinaryAsync(BinaryPacketTypes type, object message, bool forceTcp = false) {
        var json = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));
        await PostBinaryAsync(type, json, forceTcp);
    }

    /// <summary>
    /// Sends a binary packet, preferring UDP when available. Set
    /// <paramref name="forceTcp"/> for payloads that need reliable delivery
    /// (e.g. large images).
    /// </summary>
    public async Task PostBinaryAsync(BinaryPacketTypes type, byte[] content, bool forceTcp = false) {
        if (!forceTcp && _isUdpAvailable && _udp != null) {
            try {
                var packet = UdpPacket.Build(type, content);
                await _udp.SendAsync(packet, packet.Length);
                return;
            } catch (Exception e) {
                Plugin.Log.Debug($"UDP send failed, falling back to TCP: {e.Message}");
            }
        }

        if (_socket == null || _socket.State != WebSocketState.Open || _cancellationTokenSource == null) {
            return;
        }

        var framed = new BinaryPacket(type, content);
        await _socket.SendAsync(new ArraySegment<byte>(framed.Payload), WebSocketMessageType.Binary, true, _cancellationTokenSource.Token);
    }

    private void ResetUdp() {
        _isUdpAvailable = false;
        try {
            _udp?.Close();
        } catch { }
        _udp = null;
    }

    public void Dispose() {
        ResetUdp();
        _httpClient.Dispose();
        _socket?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}
