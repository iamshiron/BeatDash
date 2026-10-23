namespace Shiron.BeatDash.API.Configuration;

/// <summary>Configures the endpoint advertised to Honami Sensor Proxy clients.</summary>
public sealed class HspOptions {
    /// <summary>
    /// Public absolute ingest URL, including any reverse-proxy path. Leave empty to advertise the
    /// server's detected HTTP LAN address.
    /// </summary>
    public string? PublicIngestUrl { get; set; }
}
