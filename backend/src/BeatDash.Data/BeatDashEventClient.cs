using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Shiron.BeatDash.Data.Models;

namespace Shiron.BeatDash.Data;

public class BeatDashEventClient {
    private readonly string _host;
    private readonly object _lock = new();
    private MapSessionData? _currentSession;

    public ConcurrentDictionary<string, EndpointStats> Endpoints { get; } = new();

    public event EventHandler<string>? OnMapEvent;
    public event EventHandler<string>? OnLiveEvent;
    public event EventHandler<MapStartedEventArgs>? OnMapStarted;
    public event EventHandler<MapEndedEventArgs>? OnMapFinished;
    public event EventHandler<MapEndedEventArgs>? OnMapFailed;
    public event EventHandler<MapEndedEventArgs>? OnMapSuccess;
    public event EventHandler<MapEndedEventArgs>? OnMapEnded;
    public event EventHandler<ConnectionStateChangedEventArgs>? OnConnectionStateChanged;

    public MapSessionData? CurrentSession {
        get { lock (_lock) { return _currentSession; } }
    }

    public BeatDashEventClient(string host) {
        _host = host;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default) {
        var endpoints = new (string Name, string Uri)[] {
            ("MapData", $"{_host}/BSDataPuller/MapData"),
            ("LiveData", $"{_host}/BSDataPuller/LiveData")
        };

        foreach (var (name, _) in endpoints)
            Endpoints.GetOrAdd(name, n => new EndpointStats(n));

        var tasks = endpoints
            .Select(ep => ReceiveLoopAsync(ep.Uri, ep.Name, cancellationToken))
            .ToArray();

        await Task.WhenAll(tasks);
    }

    private async Task ReceiveLoopAsync(string uri, string endpointName, CancellationToken cancellationToken) {
        while (!cancellationToken.IsCancellationRequested) {
            using var ws = new ClientWebSocket();
            var endpoint = Endpoints.GetOrAdd(endpointName, n => new EndpointStats(n));

            SetConnectionStatus(endpoint, ConnectionStatus.Connecting, endpointName);

            try {
                await ws.ConnectAsync(new Uri(uri), cancellationToken);
                SetConnectionStatus(endpoint, ConnectionStatus.Connected, endpointName);

                var buffer = new byte[8192];

                while (ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested) {
                    var messageBuilder = new StringBuilder();
                    WebSocketReceiveResult result;

                    do {
                        result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                        messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close) {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text) {
                        var rawMessage = messageBuilder.ToString();
                        endpoint.MessageCount++;
                        endpoint.LastMessageTime = DateTimeOffset.UtcNow;
                        ProcessMessage(endpointName, rawMessage);
                    }
                }
            } catch (OperationCanceledException) {
                SetConnectionStatus(endpoint, ConnectionStatus.Disconnected, endpointName);
                break;
            } catch (WebSocketException) {
                SetConnectionStatus(endpoint, ConnectionStatus.Reconnecting, endpointName);
                try {
                    await Task.Delay(5000, cancellationToken);
                } catch (OperationCanceledException) {
                    SetConnectionStatus(endpoint, ConnectionStatus.Disconnected, endpointName);
                    break;
                }
            } catch (Exception) {
                SetConnectionStatus(endpoint, ConnectionStatus.Disconnected, endpointName);
                break;
            }
        }
    }

    private void SetConnectionStatus(EndpointStats endpoint, ConnectionStatus status, string endpointName) {
        endpoint.Status = status;
        OnConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs {
            EndpointName = endpointName,
            Status = status
        });
    }

    private void ProcessMessage(string endpointName, string rawMessage) {
        try {
            if (endpointName == "MapData") {
                OnMapEvent?.Invoke(this, rawMessage);
                ProcessMapData(rawMessage);
            } else if (endpointName == "LiveData") {
                OnLiveEvent?.Invoke(this, rawMessage);
                ProcessLiveData(rawMessage);
            }
        } catch { }
    }

    private void ProcessMapData(string rawMessage) {
        var mapData = JsonSerializer.Deserialize<MapData>(rawMessage);
        if (mapData?.Hash == null) return;

        MapStartedEventArgs? startedArgs = null;
        MapEndedEventArgs? endedArgs = null;
        MapEndReason? endReason = null;

        lock (_lock) {
            var isEndSignal = mapData.LevelFinished || mapData.LevelQuit
                || (mapData.LevelFailed && !mapData.Modifiers.NoFailOn0Energy);

            if (isEndSignal) {
                if (_currentSession != null) {
                    _currentSession.EndedAt = DateTimeOffset.UtcNow;
                    _currentSession.FullCleared = mapData.LevelFinished;
                    _currentSession.EndReason = mapData.LevelFinished ? MapEndReason.Finished
                        : mapData.LevelQuit ? MapEndReason.Quit
                        : MapEndReason.Failed;

                    FinalizeSession(_currentSession);

                    endReason = _currentSession.EndReason;
                    endedArgs = new MapEndedEventArgs { Session = _currentSession };
                    _currentSession = null;
                }
            } else if (_currentSession == null) {
                if (!mapData.InLevel)
                    return;

                _currentSession = new MapSessionData {
                    Hash = mapData.Hash,
                    SongName = mapData.SongName,
                    SongSubName = mapData.SongSubName,
                    SongAuthor = mapData.SongAuthor,
                    Mapper = mapData.Mapper,
                    BSRKey = mapData.BSRKey,
                    CoverImage = mapData.CoverImage,
                    Duration = mapData.Duration,
                    MapType = mapData.MapType,
                    Difficulty = mapData.Difficulty,
                    CustomDifficultyLabel = mapData.CustomDifficultyLabel,
                    BPM = mapData.BPM,
                    NJS = mapData.NJS,
                    Modifiers = mapData.Modifiers,
                    ModifiersMultiplier = mapData.ModifiersMultiplier,
                    PracticeMode = mapData.PracticeMode,
                    PracticeModeModifiers = mapData.PracticeModeModifiers,
                    PP = mapData.PP,
                    Star = mapData.Star,
                    GameVersion = mapData.GameVersion,
                    PluginVersion = mapData.PluginVersion,
                    IsMultiplayer = mapData.IsMultiplayer,
                    PreviousRecord = mapData.PreviousRecord,
                    PreviousBSR = mapData.PreviousBSR,
                    StartedAt = DateTimeOffset.UtcNow,
                    NoFailEnabled = mapData.Modifiers.NoFailOn0Energy,
                };

                startedArgs = new MapStartedEventArgs { Session = _currentSession };
            } else {
                _currentSession.IsPaused = mapData.LevelPaused;
            }
        }

        if (startedArgs != null)
            OnMapStarted?.Invoke(this, startedArgs);

        if (endedArgs != null && endReason != null) {
            switch (endReason.Value) {
                case MapEndReason.Finished:
                    OnMapFinished?.Invoke(this, endedArgs);
                    OnMapSuccess?.Invoke(this, endedArgs);
                    break;
                case MapEndReason.Failed:
                    OnMapFailed?.Invoke(this, endedArgs);
                    break;
                case MapEndReason.Quit:
                    OnMapEnded?.Invoke(this, endedArgs);
                    break;
            }
        }
    }

    private void ProcessLiveData(string rawMessage) {
        var liveData = JsonSerializer.Deserialize<LiveData>(rawMessage);
        if (liveData == null) return;

        lock (_lock) {
            if (_currentSession == null) return;

            _currentSession.Snapshots.Add(new LiveDataSnapshot {
                Timestamp = DateTimeOffset.UtcNow,
                TimeElapsed = liveData.TimeElapsed,
                Score = liveData.Score,
                ScoreWithMultipliers = liveData.ScoreWithMultipliers,
                MaxScore = liveData.MaxScore,
                MaxScoreWithMultipliers = liveData.MaxScoreWithMultipliers,
                Accuracy = liveData.Accuracy,
                PlayerHealth = liveData.PlayerHealth,
                Combo = liveData.Combo,
                Misses = liveData.Misses,
                NotesSpawned = liveData.NotesSpawned,
                FullCombo = liveData.FullCombo,
                Rank = liveData.Rank,
                BlockHitScore = liveData.BlockHitScore,
            });
        }
    }

    private static void FinalizeSession(MapSessionData session) {
        var last = session.Snapshots.LastOrDefault();
        if (last == null) return;

        session.FinalScore = last.Score;
        session.FinalScoreWithMultipliers = last.ScoreWithMultipliers;
        session.MaxScore = last.MaxScore;
        session.MaxScoreWithMultipliers = last.MaxScoreWithMultipliers;
        session.FinalAccuracy = last.Accuracy;
        session.FinalRank = last.Rank;
        session.FinalCombo = last.Combo;
        session.FinalMisses = last.Misses;
        session.NotesSpawned = last.NotesSpawned;
        session.FullCombo = last.FullCombo;
        session.FinalPlayerHealth = last.PlayerHealth;
        session.FinalBlockHitScore = last.BlockHitScore;
        session.DurationPlayed = last.TimeElapsed;
    }
}
