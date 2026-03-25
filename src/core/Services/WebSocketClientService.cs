using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Shiron.BeatDash.API.Models;

namespace Shiron.BeatDash.API.Services;

public class WebSocketClientService : BackgroundService {
    readonly ILogger<WebSocketClientService> _logger;
    readonly ConcurrentDictionary<string, WebSocketConnection> _connections = new();

    readonly (string Name, string Uri)[] _endpoints = [
        ("MapData", "ws://127.0.0.1:2946/BSDataPuller/MapData"),
        ("LiveData", "ws://127.0.0.1:2946/BSDataPuller/LiveData")
    ];

    MapData? _currentMapData;
    LiveData? _currentLiveData;
    bool _mapInProgress;
    readonly object _stateLock = new();

    public event EventHandler<WebSocketMessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<MapData>? NewMapStarted;
    public event EventHandler<MapFinishedEventArgs>? MapFinished;

    public WebSocketClientService(ILogger<WebSocketClientService> logger) {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        foreach (var endpoint in _endpoints) {
            var connection = new WebSocketConnection(endpoint.Name, endpoint.Uri);
            _connections[endpoint.Name] = connection;

            _ = Task.Run(() => ConnectAndReceiveAsync(connection, stoppingToken), stoppingToken);
        }

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    async Task ConnectAndReceiveAsync(WebSocketConnection connection, CancellationToken stoppingToken) {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            connection.CancellationTokenSource.Token,
            stoppingToken
        );

        while (!stoppingToken.IsCancellationRequested) {
            try {
                _logger.LogInformation("Connecting to {Name} at {Uri}...", connection.Name, connection.Uri);
                await connection.Client.ConnectAsync(connection.Uri, linkedCts.Token);

                _logger.LogInformation("Connected to {Name}", connection.Name);
                await ReceiveMessagesAsync(connection, linkedCts.Token);
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                _logger.LogError(ex, "Connection error for {Name}. Reconnecting in 5 seconds...", connection.Name);
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    async Task ReceiveMessagesAsync(WebSocketConnection connection, CancellationToken cancellationToken) {
        var buffer = new byte[8192];
        var messageBuilder = new StringBuilder();

        while (connection.Client.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested) {
            WebSocketReceiveResult result;
            messageBuilder.Clear();

            do {
                result = await connection.Client.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            } while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Close) {
                _logger.LogInformation("WebSocket {Name} closed by server", connection.Name);
                await connection.Client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
                break;
            }

            if (result.MessageType == WebSocketMessageType.Text) {
                var message = messageBuilder.ToString();
                _logger.LogDebug("Received from {Name}: {Message}", connection.Name, message);

                MessageReceived?.Invoke(this, new WebSocketMessageReceivedEventArgs(connection.Name, message));

                if (connection.Name == "MapData") {
                    ProcessMapData(message);
                } else if (connection.Name == "LiveData") {
                    ProcessLiveData(message);
                }
            }
        }
    }

    void ProcessMapData(string message) {
        try {
            var mapData = JsonSerializer.Deserialize<MapData>(message);
            if (mapData == null) return;

            lock (_stateLock) {
                var wasInProgress = _mapInProgress;
                var currentHash = _currentMapData?.Hash;
                var isNewMap = mapData.Hash != null && mapData.Hash != currentHash;

                if (isNewMap && !mapData.LevelFinished && !mapData.LevelFailed && !mapData.LevelQuit) {
                    _currentMapData = mapData;
                    _mapInProgress = true;
                    LogNewMap(mapData);
                    NewMapStarted?.Invoke(this, mapData);
                } else if (wasInProgress && (mapData.LevelFinished || mapData.LevelFailed || mapData.LevelQuit)) {
                    _mapInProgress = false;
                    _currentMapData = null;
                    LogMapFinished(mapData);
                    MapFinished?.Invoke(this, new MapFinishedEventArgs(mapData, _currentLiveData));
                }
            }
        } catch (JsonException ex) {
            _logger.LogError(ex, "Failed to deserialize MapData");
        }
    }

    void ProcessLiveData(string message) {
        try {
            var liveData = JsonSerializer.Deserialize<LiveData>(message);
            if (liveData == null) return;

            lock (_stateLock) {
                _currentLiveData = liveData;
            }
        } catch (JsonException ex) {
            _logger.LogError(ex, "Failed to deserialize LiveData");
        }
    }

    void LogNewMap(MapData map) {
        _logger.LogInformation(
            "New map started: {SongName} by {SongAuthor} | Mapper: {Mapper} | Difficulty: {Difficulty} | BSR: {BSRKey}",
            map.SongName, map.SongAuthor, map.Mapper, map.Difficulty, map.BSRKey ?? "N/A"
        );
    }

    void LogMapFinished(MapData map) {
        var live = _currentLiveData;
        var endReason = map.LevelFinished ? "Finished" : map.LevelFailed ? "Failed" : "Quit";
        var duration = TimeSpan.FromSeconds(map.Duration);
        var timeElapsed = TimeSpan.FromSeconds(live?.TimeElapsed ?? 0);

        _logger.LogInformation(
            "Map {EndReason}: {SongName} by {SongAuthor} | Time: {TimeElapsed:mm\\:ss} / {Duration:mm\\:ss} | Score: {Score} ({Rank}) | Accuracy: {Accuracy:F2}% | Combo: {Combo} | Misses: {Misses} | FC: {FullCombo}",
            endReason, map.SongName, map.SongAuthor, timeElapsed, duration,
            live?.Score ?? 0, live?.Rank ?? "N/A",
            live?.Accuracy ?? 100,
            live?.Combo ?? 0, live?.Misses ?? 0,
            live?.FullCombo ?? false
        );
    }

    public async Task SendAsync(string connectionName, string message, CancellationToken cancellationToken = default) {
        if (_connections.TryGetValue(connectionName, out var connection) &&
            connection.Client.State == WebSocketState.Open) {
            var bytes = Encoding.UTF8.GetBytes(message);
            await connection.Client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken) {
        foreach (var connection in _connections.Values) {
            if (connection.Client.State == WebSocketState.Open) {
                await connection.Client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Service stopping", cancellationToken);
            }
            connection.CancellationTokenSource.Cancel();
            connection.Client.Dispose();
            connection.CancellationTokenSource.Dispose();
        }

        await base.StopAsync(cancellationToken);
    }
}

public class WebSocketMessageReceivedEventArgs(string connectionName, string message) : EventArgs {
    public string ConnectionName { get; } = connectionName;
    public string Message { get; } = message;
}

public class MapFinishedEventArgs(MapData mapData, LiveData? liveData) : EventArgs {
    public MapData MapData { get; } = mapData;
    public LiveData? LiveData { get; } = liveData;
}
