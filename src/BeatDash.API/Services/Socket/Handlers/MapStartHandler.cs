using Microsoft.Extensions.Logging;
using Shiron.BeatDash.Data.Socket;

namespace Shiron.BeatDash.API.Services.Socket.Handlers;

/// <summary>
/// Handles <see cref="MapStartMessage"/> received from the client when a beatmap starts.
/// </summary>
public sealed class MapStartHandler(ILogger<MapStartHandler> logger)
    : SocketMessageHandler<MapStartMessage> {

    protected override Task HandleMessageAsync(
        SocketContext context, MapStartMessage message, CancellationToken ct) {
        logger.LogInformation("Map started: {SongName}", message.SongName);
        return Task.CompletedTask;
    }
}
