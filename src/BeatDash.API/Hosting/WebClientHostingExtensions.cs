namespace Shiron.BeatDash.API.Hosting;

/// <summary>Hosts the bundled dashboard alongside the API.</summary>
public static class WebClientHostingExtensions {
    /// <summary>Serves dashboard assets and client-side routes without masking missing API endpoints.</summary>
    public static void UseWebClient(this WebApplication app) {
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapFallback("/api/{**path}", () => Results.NotFound());
        app.MapFallbackToFile("index.html");
    }
}
