using Microsoft.Extensions.Options;
using Shiron.BeatDash.API.Configuration;

namespace Shiron.BeatDash.API.Endpoints;

/// <summary>Exposes the running server's status and build version.</summary>
public static class StatusEndpoints {
    /// <summary>Maps the public status endpoint.</summary>
    public static void MapStatusEndpoints(this IEndpointRouteBuilder endpoints) {
        var registration = endpoints.ServiceProvider.GetRequiredService<IOptions<RegistrationOptions>>().Value;
        endpoints.MapGet("/health", () => GetStatus(registration.DisableSignup)).WithTags("Health");
    }

    internal static StatusDto GetStatus(bool disableSignup = false) => new("OK", GitVersionInformation.SemVer, !disableSignup);
}
