using Shiron.BeatDash.API.Data;
using Shiron.BeatDash.API.Models;

namespace Shiron.BeatDash.API.Services;

public interface IEventStorageService {
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}

public class EventStorageService : IEventStorageService {
    private readonly WebSocketClientService _webSocketService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EventStorageService> _logger;

    private long? _currentPlaySessionId;
    private readonly object _sessionLock = new();

    public EventStorageService(
        WebSocketClientService webSocketService,
        IServiceScopeFactory scopeFactory,
        ILogger<EventStorageService> logger) {
        _webSocketService = webSocketService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken) {
        _webSocketService.MessageReceived += OnMessageReceived;
        _webSocketService.NewMapStarted += OnNewMapStarted;
        _webSocketService.MapFinished += OnMapFinished;
        _webSocketService.CoverImageReceived += OnCoverImageReceived;

        _logger.LogInformation("EventStorageService started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) {
        _webSocketService.MessageReceived -= OnMessageReceived;
        _webSocketService.NewMapStarted -= OnNewMapStarted;
        _webSocketService.MapFinished -= OnMapFinished;
        _webSocketService.CoverImageReceived -= OnCoverImageReceived;

        _logger.LogInformation("EventStorageService stopped");
        return Task.CompletedTask;
    }

    private async void OnMessageReceived(object? sender, WebSocketMessageReceivedEventArgs e) {
        try {
            using var scope = _scopeFactory.CreateScope();
            var sqliteService = scope.ServiceProvider.GetRequiredService<ISqliteService>();
            long? sessionId;
            lock (_sessionLock) {
                sessionId = _currentPlaySessionId;
            }
            await sqliteService.AddRawMessageAsync(e.ConnectionName, e.Message, sessionId);
        } catch (Exception ex) {
            _logger.LogError(ex, "Failed to store raw message");
        }
    }

    private async void OnNewMapStarted(object? sender, MapData e) {
        try {
            using var scope = _scopeFactory.CreateScope();
            var sqliteService = scope.ServiceProvider.GetRequiredService<ISqliteService>();
            var session = await sqliteService.CreatePlaySessionAsync(e);
            lock (_sessionLock) {
                _currentPlaySessionId = session.Id;
            }
            _logger.LogDebug("New play session created: {Id}", session.Id);
        } catch (Exception ex) {
            _logger.LogError(ex, "Failed to create play session");
        }
    }

    private async void OnMapFinished(object? sender, MapFinishedEventArgs e) {
        try {
            long? sessionId;
            lock (_sessionLock) {
                sessionId = _currentPlaySessionId;
            }

            if (sessionId.HasValue) {
                using var scope = _scopeFactory.CreateScope();
                var sqliteService = scope.ServiceProvider.GetRequiredService<ISqliteService>();
                await sqliteService.UpdatePlaySessionFinalDataAsync(
                    sessionId.Value,
                    e.MapData,
                    e.LiveData
                );
                _logger.LogDebug("Play session {Id} finished", sessionId);
                lock (_sessionLock) {
                    _currentPlaySessionId = null;
                }
            }
        } catch (Exception ex) {
            _logger.LogError(ex, "Failed to update play session final data");
        }
    }

    private async void OnCoverImageReceived(object? sender, MapData e) {
        try {
            if (string.IsNullOrEmpty(e.Hash)) return;
            using var scope = _scopeFactory.CreateScope();
            var sqliteService = scope.ServiceProvider.GetRequiredService<ISqliteService>();
            await sqliteService.UpdateMapCoverImageAsync(e.Hash, e.CoverImage);
        } catch (Exception ex) {
            _logger.LogError(ex, "Failed to update map cover image");
        }
    }
}

public class EventStorageServiceHostedService : BackgroundService {
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EventStorageServiceHostedService> _logger;
    private IEventStorageService? _eventStorageService;

    public EventStorageServiceHostedService(
        IServiceProvider serviceProvider,
        ILogger<EventStorageServiceHostedService> logger) {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        using var scope = _serviceProvider.CreateScope();
        _eventStorageService = scope.ServiceProvider.GetRequiredService<IEventStorageService>();
        await _eventStorageService.StartAsync(stoppingToken);

        try {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        } catch (OperationCanceledException) {
        }

        await _eventStorageService.StopAsync(stoppingToken);
    }
}
