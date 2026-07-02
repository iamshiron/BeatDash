namespace Shiron.BeatDash.API.Services.Socket;

/// <summary>
/// Provides context about the active socket connection to message handlers.
/// </summary>
/// <param name="UserId">The authenticated user's ID.</param>
/// <param name="ClientId">The connected device's client ID.</param>
/// <param name="SessionId">The active socket session ID, used for sending responses back.</param>
/// <param name="SessionManager">The session manager for sending messages to the client.</param>
public sealed record SocketContext(
    Guid UserId,
    Guid ClientId,
    Guid SessionId,
    ISessionManager SessionManager
);
