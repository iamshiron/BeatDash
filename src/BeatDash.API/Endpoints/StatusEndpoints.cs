namespace Shiron.BeatDash.API.Endpoints;

/// <summary>Exposes the running server's status and build version.</summary>
public static class StatusEndpoints {
    /// <summary>Maps the public status endpoint.</summary>
    public static void MapStatusEndpoints(this IEndpointRouteBuilder endpoints) {
        endpoints.MapGet("/health", GetStatus).WithTags("Health");
    }

    internal static StatusDto GetStatus() => new("OK", GitVersionInformation.SemVer);
}
