using Shiron.BeatDash.API.Services;

namespace Shiron.BeatDash.API.Endpoints;

public static class StatusEndpoints {
    public static RouteGroupBuilder MapStatusApi(this RouteGroupBuilder group) {
        group.MapGet("/", GetStatus)
            .WithName("GetStatus")
            .WithDescription("Get server status including Beat Saber connection state and current gameplay info");

        group.MapGet("/health", GetHealth)
            .WithName("GetHealth")
            .WithDescription("Simple health check endpoint");

        return group;
    }

    private static IResult GetStatus(WebSocketClientService webSocketService) {
        var status = webSocketService.GetStatus();
        return Results.Ok(status);
    }

    private static IResult GetHealth() {
        return Results.Ok(new {
            Status = "Healthy",
            Timestamp = DateTimeOffset.UtcNow
        });
    }
}
