using Microsoft.Extensions.Options;
using Shiron.BeatDash.API.Configuration;

namespace Shiron.BeatDash.API.Endpoints;

/// <summary>Exposes the running server's status and build version.</summary>
public static class StatusEndpoints {
    /// <summary>Maps the public status endpoint.</summary>
    public static void MapStatusEndpoints(this IEndpointRouteBuilder endpoints) {
        var registration = endpoints.ServiceProvider.GetRequiredService<IOptions<RegistrationOptions>>().Value;
        var desktop = endpoints.ServiceProvider.GetRequiredService<IOptions<DesktopOptions>>().Value.Enabled;
        endpoints.MapGet("/health", () => GetStatus(registration.DisableSignup, desktop)).WithTags("Health");
    }

    internal static StatusDto GetStatus(bool disableSignup = false, bool desktop = false) =>
        new("OK", GitVersionInformation.SemVer, !disableSignup && !desktop, desktop);
}
