using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Shiron.BeatDash.API.DTOs.Socket;

namespace Shiron.BeatDash.API.Services;

public interface ISessionManager {
    Session AddSession(Guid userId, Guid clientId, WebSocket socket);
    bool TryRemoveSession(Guid sessionId);
    Session? GetSession(Guid userId);

    Task SendBytesToClientAsync(Guid sessionId, byte[] payload, CancellationToken ct);
    Task SendMessageAsync<T>(Guid sessionId, T message, CancellationToken ct) where T : SocketMessage<T>;
}

public class SessionManager : ISessionManager {
    private readonly Encoding _socketEncoding = new UTF8Encoding(false, true);

    /// <summary>
    /// Stores all active sessions.
    /// Key: Session ID
    /// Value: <see cref="Session"/>
    /// </summary>
    private readonly ConcurrentDictionary<Guid, Session> _sessions = [];

    /// <summary>
    /// Stores all active user sessions.
    /// Key: User ID
    /// Value: Session ID
    /// </summary>
    private readonly ConcurrentDictionary<Guid, Guid> _userSessions = [];

    public Session AddSession(Guid userId, Guid clientId, WebSocket socket) {
        // If user already has a session, remove it first
        if (_userSessions.TryGetValue(userId, out var existingSessionId)) {
            TryRemoveSession(existingSessionId);
        }

        var session = new Session {
            Id = Guid.NewGuid(),
            UserId = userId,
            ClientId = clientId,
            CreatedAt = DateTime.UtcNow,
            Socket = socket
        };

        // Save to both indexes
        _sessions[session.Id] = session;
        _userSessions[userId] = session.Id;

        return session;
    }

    public bool TryRemoveSession(Guid sessionId) {
        if (_sessions.TryRemove(sessionId, out var session)) {
            _userSessions.TryRemove(session.UserId, out _);
            return true;
        }
        return false;
    }

    public Session? GetSession(Guid userId) {
        if (_userSessions.TryGetValue(userId, out var sessionId)) {
            if (_sessions.TryGetValue(sessionId, out var session)) {
                return session;
            }
        }
        return null;
    }

    public async Task SendBytesToClientAsync(Guid sessionId, byte[] payload, CancellationToken ct = default) {
        if (_sessions.TryGetValue(sessionId, out var session)) {
            await session.SendLock.WaitAsync(ct);
            try {
                await session.Socket.SendAsync(
                    new ArraySegment<byte>(payload),
                    WebSocketMessageType.Text,
                    true,
                    ct
                );
            } finally {
                session.SendLock.Release();
            }
        }
    }

    public Task SendMessageAsync<T>(Guid sessionId, T message, CancellationToken ct) where T : SocketMessage<T> {
        var utf8Bytes = JsonSerializer.SerializeToUtf8Bytes(message);
        return SendBytesToClientAsync(sessionId, utf8Bytes, ct);
    }
}

public record Session {
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required Guid ClientId { get; init; }

    public required DateTime CreatedAt { get; init; }
    public required WebSocket Socket { get; init; }

    public SemaphoreSlim SendLock { get; } = new(1, 1);
}
