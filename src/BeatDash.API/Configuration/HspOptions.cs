namespace Shiron.BeatDash.API.Configuration;

/// <summary>Configures the endpoint advertised to Honami Sensor Proxy clients.</summary>
public sealed class HspOptions {
    /// <summary>
    /// Public absolute ingest URL, including any reverse-proxy path. Leave empty to advertise the
    /// server's detected HTTP LAN address.
    /// </summary>
    public string? PublicIngestUrl { get; set; }

    /// <summary>Returns the configured public URL, or <see langword="null"/> when it is blank.</summary>
    /// <exception cref="InvalidOperationException">The configured URL is not absolute HTTP or HTTPS.</exception>
    public Uri? GetPublicIngestUri() {
        if (string.IsNullOrWhiteSpace(PublicIngestUrl)) return null;

        var value = PublicIngestUrl.Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("Hsp:PublicIngestUrl must be an absolute HTTP or HTTPS URL.");

        return uri;
    }
}
