using System.Buffers;
using System.Net.WebSockets;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Shiron.BeatDash.API.Services;
using Shiron.BeatDash.API.Services.Realtime;
using Shiron.BeatDash.API.Services.Socket;
using Shiron.BeatDash.Data.Realtime.Events;
using Shiron.BeatDash.Data.Socket;
using Shiron.BeatDash.DB;

namespace Shiron.BeatDash.API.Endpoints;

public static class ClientEndpoints {
    private const int ReceiveBufferSize = 8192;

    public static void MapClientEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup("/client").WithTags("Client");

        group.MapGet("/ws", async (
            HttpContext context,
            ISessionManager sessionManager,
            IRealtimeBroadcaster broadcaster,
            IHostApplicationLifetime appLifetime,
            SocketMessageDispatcher messageDispatcher,
            SocketBinaryDispatcher binaryDispatcher,
            BeatDashDbContext db,
            CancellationToken ct) => {
                if (!context.WebSockets.IsWebSocketRequest) {
                    return Results.BadRequest();
                }

                var userId = IdentityUtils.GetUserID(context.User);
                if (!userId.HasValue) {
                    return Results.Unauthorized();
                }

                var clientIdHeader = context.Request.Headers["X-Client-Id"].FirstOrDefault();
                if (string.IsNullOrEmpty(clientIdHeader) || !Guid.TryParse(clientIdHeader, out var clientId)) {
                    return Results.BadRequest();
                }

                using var socket = await context.WebSockets.AcceptWebSocketAsync();
                var session = sessionManager.AddSession(userId.Value, clientId, socket);

                await db.Devices.Where(d => d.ClientId == clientId && d.UserId == userId)
                    .ExecuteUpdateAsync(s => s
                            .SetProperty(p => p.LastSeenAt, DateTime.UtcNow), ct
                    );

                await broadcaster.SendDeviceStatusAsync(userId.Value, new DeviceStatusEvent(clientId, true, DateTime.UtcNow));

                var socketContext = new SocketContext(userId.Value, clientId, session.Id, sessionManager);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                    context.RequestAborted,
                    appLifetime.ApplicationStopping
                );

                try {
                    await ReceiveLoopAsync(socket, socketContext, messageDispatcher, binaryDispatcher, cts.Token);
                } catch (WebSocketException) {
                    socket.Abort();
                } catch (OperationCanceledException) {
                    // Connection cancelled — normal shutdown
                } finally {
                    sessionManager.TryRemoveSession(session.Id);
                    await broadcaster.SendDeviceStatusAsync(userId.Value, new DeviceStatusEvent(clientId, false, DateTime.UtcNow));
                }

                return Results.Empty;
            }).RequireAuthorization().ExcludeFromDescription();

        group.MapPost("/ping/{clientId:Guid}", async (
            ISessionManager sessionManager,
            ClaimsPrincipal user,
            BeatDashDbContext db,
            Guid clientId) => {
                var userId = IdentityUtils.GetUserID(user);
                if (!userId.HasValue) return Results.Unauthorized();

                var device = await db.Devices.FirstOrDefaultAsync(d => d.UserId == userId.Value && d.ClientId == clientId);
                var session = sessionManager.GetSession(userId.Value);
                if (session == null) return Results.NotFound();
                if (session.ClientId != device?.ClientId) return Results.NotFound();

                var payload = new PingRequestMessage {
                    Message = "Test"
                };

                await sessionManager.SendMessageAsync(session.Id, payload, CancellationToken.None);
                return Results.Ok();
            }).RequireAuthorization();
    }

    /// <summary>
    /// Receives WebSocket messages, reassembling fragmented frames, and dispatches
    /// each complete message to the appropriate dispatcher.
    /// </summary>
    private static async Task ReceiveLoopAsync(
        WebSocket socket,
        SocketContext socketContext,
        SocketMessageDispatcher messageDispatcher,
        SocketBinaryDispatcher binaryDispatcher,
        CancellationToken ct) {

        var buffer = ArrayPool<byte>.Shared.Rent(ReceiveBufferSize);
        var messageStream = new MemoryStream();

        try {
            while (true) {
                messageStream.SetLength(0);

                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                if (result.MessageType == WebSocketMessageType.Close) {
                    await socket.CloseAsync(
                        result.CloseStatus ?? WebSocketCloseStatus.NormalClosure,
                        result.CloseStatusDescription,
                        CancellationToken.None
                    );
                    return;
                }

                messageStream.Write(buffer, 0, result.Count);

                while (!result.EndOfMessage) {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    messageStream.Write(buffer, 0, result.Count);
                }

                var payload = messageStream.GetBuffer().AsMemory(0, (int) messageStream.Length);

                switch (result.MessageType) {
                    case WebSocketMessageType.Text:
                        await messageDispatcher.DispatchAsync(socketContext, payload, ct);
                        break;
                    case WebSocketMessageType.Binary:
                        await binaryDispatcher.DispatchAsync(socketContext, payload, ct);
                        break;
                }
            }
        } finally {
            messageStream.Dispose();
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
