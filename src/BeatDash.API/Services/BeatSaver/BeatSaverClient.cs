using System.Net;
using System.Text.Json;

namespace Shiron.BeatDash.API.Services.BeatSaver;

/// <summary>
/// Thin client over the BeatSaver API. All calls pass through the shared
/// <see cref="BeatSaverRateLimiter"/> so the configured request budget is never
/// exceeded, regardless of who triggered the fetch.
/// </summary>
public interface IBeatSaverClient {
    /// <summary>
    /// Fetches a map by its file <paramref name="hash"/>, or <see langword="null"/>
    /// if BeatSaver has no such map (HTTP 404). Throws on other failures.
    /// </summary>
    Task<BeatSaverMapResponse?> GetMapByHashAsync(string hash, CancellationToken ct);

    /// <summary>
    /// Downloads the bytes at an absolute BeatSaver CDN <paramref name="url"/>, or
    /// <see langword="null"/> if the object is missing (HTTP 404). Throws otherwise.
    /// </summary>
    Task<byte[]?> DownloadAsync(string url, CancellationToken ct);
}

/// <summary>
/// <see cref="HttpClient"/>-backed implementation; the client is configured
/// (base address, user-agent, timeout) at registration time.
/// </summary>
public sealed class BeatSaverClient(
    HttpClient http,
    BeatSaverRateLimiter rateLimiter,
    ILogger<BeatSaverClient> logger
) : IBeatSaverClient {

    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc/>
    public async Task<BeatSaverMapResponse?> GetMapByHashAsync(string hash, CancellationToken ct) {
        await rateLimiter.WaitAsync(ct);

        using var response = await http.GetAsync($"/maps/hash/{hash}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) {
            logger.LogInformation("BeatSaver has no map for hash {Hash}", hash);
            return null;
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<BeatSaverMapResponse>(stream, JsonOptions, ct);
    }

    /// <inheritdoc/>
    public async Task<byte[]?> DownloadAsync(string url, CancellationToken ct) {
        await rateLimiter.WaitAsync(ct);

        using var response = await http.GetAsync(url, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) {
            logger.LogWarning("BeatSaver download 404 for {Url}", url);
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(ct);
    }
}
