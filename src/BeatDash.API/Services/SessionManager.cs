using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Shiron.BeatDash.Data.Socket;

namespace Shiron.BeatDash.API.Services;

public interface ISessionManager {
    /// <summary>
    /// Registers a new WebSocket session for a user, replacing any existing one.
    /// </summary>
    Session AddSession(Guid userId, Guid clientId, WebSocket socket);

    /// <summary>
    /// Removes the session and cleans up its user and UDP endpoint indexes.
    /// </summary>
    /// <returns><c>true</c> if the session existed and was removed; otherwise <c>false</c>.</returns>
    bool TryRemoveSession(Guid sessionId);

    /// <summary>
    /// Retrieves the active session for the specified user.
    /// </summary>
    /// <returns>The session, or <c>null</c> if the user has no active session.</returns>
    Session? GetSession(Guid userId);

    /// <summary>
    /// Sends raw bytes to a client over its session WebSocket. No-ops if the session is gone.
    /// </summary>
    Task SendBytesToClientAsync(Guid sessionId, byte[] payload, CancellationToken ct = default);

    /// <summary>
    /// Serializes a message to UTF-8 JSON and sends it to the client.
    /// </summary>
    Task SendMessageAsync<T>(Guid sessionId, T message, CancellationToken ct) where T : SocketMessage<T>;

    /// <summary>
    /// Creates a single-use ticket a client can present to bind a UDP endpoint to its session.
    /// </summary>
    /// <exception cref="InvalidOperationException">The session does not exist.</exception>
    Guid GenerateUdpTicket(Guid sessionId);

    /// <summary>
    /// Consumes a one-time ticket and binds the sender's UDP endpoint to its session.
    /// </summary>
    /// <returns>The authenticated session, or <c>null</c> if the ticket or session is invalid.</returns>
    Session? AuthenticateUdp(Guid ticket, IPEndPoint senderEndPoint);

    /// <summary>
    /// Looks up the session associated with a UDP endpoint.
    /// </summary>
    /// <returns>The session, or <c>null</c> if no session is bound to that endpoint.</returns>
    Session? GetSessionByUdpEndPoint(IPEndPoint senderEndPoint);
}

public class SessionManager(IMemoryCache cache) : ISessionManager {
    private const string UdpTicketKey = "Ticket_";
    private const string UdpEndpointKey = "Endpoint_";
    private const int UdpTicketExpirySeconds = 30;
    private const int UdpEndpointExpiryMinutes = 15;

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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public bool TryRemoveSession(Guid sessionId) {
        if (_sessions.TryRemove(sessionId, out var session)) {
            _userSessions.TryRemove(session.UserId, out _);

            if (session.UdpEndPoint is not null) {
                cache.Remove($"{UdpEndpointKey}{session.UdpEndPoint}");
            }

            return true;
        }
        return false;
    }

    /// <inheritdoc/>
    public Session? GetSession(Guid userId) {
        if (_userSessions.TryGetValue(userId, out var sessionId) && _sessions.TryGetValue(sessionId, out var session)) {
            return session;
        }
        return null;
    }

    /// <inheritdoc/>
    public async Task SendBytesToClientAsync(Guid sessionId, byte[] payload, CancellationToken ct) {
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

    /// <inheritdoc/>
    public Task SendMessageAsync<T>(Guid sessionId, T message, CancellationToken ct) where T : SocketMessage<T> {
        var utf8Bytes = JsonSerializer.SerializeToUtf8Bytes(message);
        return SendBytesToClientAsync(sessionId, utf8Bytes, ct);
    }

    /// <inheritdoc/>
    public Guid GenerateUdpTicket(Guid sessionId) {
        if (!_sessions.ContainsKey(sessionId)) throw new InvalidOperationException("Session not found");

        var newTicket = Guid.NewGuid();
        cache.Set($"{UdpTicketKey}{newTicket}", sessionId, TimeSpan.FromSeconds(UdpTicketExpirySeconds));

        return newTicket;
    }

    /// <inheritdoc/>
    public Session? AuthenticateUdp(Guid ticket, IPEndPoint senderEndPoint) {
        var ticketKey = $"{UdpTicketKey}{ticket}";
        if (!cache.TryGetValue(ticketKey, out Guid sessionId)) return null;
        cache.Remove(ticketKey);

        if (!_sessions.TryGetValue(sessionId, out var session)) return null;
        if (session.UdpEndPoint is not null) cache.Remove($"{UdpEndpointKey}{session.UdpEndPoint}");
        session.UdpEndPoint = senderEndPoint;

        cache.Set($"{UdpEndpointKey}{senderEndPoint}", sessionId, new MemoryCacheEntryOptions {
            SlidingExpiration = TimeSpan.FromMinutes(UdpEndpointExpiryMinutes)
        });

        return session;
    }

    /// <inheritdoc/>
    public Session? GetSessionByUdpEndPoint(IPEndPoint senderEndPoint) {
        if (cache.TryGetValue($"{UdpEndpointKey}{senderEndPoint}", out Guid sessionId) &&
            _sessions.TryGetValue(sessionId, out var session)) {
            return session;
        }
        return null;
    }
}

public record Session {
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required Guid ClientId { get; init; }

    public required DateTime CreatedAt { get; init; }
    public required WebSocket Socket { get; init; }

    public SemaphoreSlim SendLock { get; } = new(1, 1);
    public IPEndPoint? UdpEndPoint { get; set; }
}
