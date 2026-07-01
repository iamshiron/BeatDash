using System.Net.WebSockets;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shiron.BeatDash.API.DTOs.Socket;
using Shiron.BeatDash.API.Services;
using Shiron.BeatDash.DB;

namespace Shiron.BeatDash.API.Endpoints;

public static class ClientEndpoints {
    public static void MapClientEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup("/client").WithTags("Client");

        group.MapGet("/ws", async (
            HttpContext context,
            ISessionManager sessionManager,
            IHostApplicationLifetime appLifetime,
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

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                    context.RequestAborted,
                    appLifetime.ApplicationStopping
                );

                try {
                    var buffer = new byte[1024 * 4];
                    var receiveResult = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);

                    while (!receiveResult.CloseStatus.HasValue) {
                        // TODO: Implement actual client to server communication
                        receiveResult = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                    }

                    await socket.CloseAsync(receiveResult.CloseStatus.Value, receiveResult.CloseStatusDescription, CancellationToken.None);
                } catch (WebSocketException) {
                    socket.Abort();
                } finally {
                    sessionManager.TryRemoveSession(session.Id);
                }

                return Results.Ok();
            }).RequireAuthorization().ExcludeFromDescription();

        group.MapPost("/ping/{clientId}", async (
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

                var payload = new PingRequestDto {
                    Message = "Test"
                };

                await sessionManager.SendMessageAsync(session.Id, payload, CancellationToken.None);
                return Results.Ok();
            }).RequireAuthorization();
    }
}
