using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.Beatmaps;
using Shiron.BeatDash.DB;

namespace Shiron.BeatDash.API.Services;

/// <summary>A map's extracted song audio, ready to stream to a client.</summary>
public sealed record MapAudio(byte[] Data, string ContentType, string Filename);

/// <summary>
/// Serves a map's song audio by extracting it from the BeatSaver zip stored in
/// object storage. The zip's <c>Info.dat</c> names the audio file (typically
/// <c>song.egg</c>, an Ogg Vorbis stream); this reads that entry out verbatim.
/// </summary>
public interface IMapAudioService {
    /// <summary>
    /// Extracts the song audio for a beatmap, or <see langword="null"/> when the map is
    /// unknown, has no downloaded zip, or the zip lacks a resolvable audio file.
    /// </summary>
    Task<MapAudio?> GetSongAsync(Guid beatmapId, CancellationToken ct);
}

/// <inheritdoc cref="IMapAudioService"/>
public sealed class MapAudioService(
    BeatDashDbContext db,
    IStorageService storage,
    IOptions<StorageOptions> storageOptions,
    ILogger<MapAudioService> logger
) : IMapAudioService {

    /// <inheritdoc/>
    public async Task<MapAudio?> GetSongAsync(Guid beatmapId, CancellationToken ct) {
        var map = await db.Beatmaps
            .AsNoTracking()
            .Where(b => b.Id == beatmapId)
            .Select(b => new { b.LevelId, ZipKey = b.BeatSaverMap!.ZipObjectKey })
            .FirstOrDefaultAsync(ct);

        if (map is null || string.IsNullOrEmpty(map.ZipKey)) return null;

        var zipBytes = await storage.DownloadAsync(storageOptions.Value.BucketAssets, map.ZipKey, ct);
        if (zipBytes is null) {
            logger.LogWarning("Map audio: zip '{Key}' missing from storage for map {MapId}", map.ZipKey, beatmapId);
            return null;
        }

        try {
            using var stream = new MemoryStream(zipBytes, writable: false);
            using var source = new ZipBeatmapFileSource(stream, map.LevelId);

            var songFilename = BeatmapParser.TryGetSongFilename(source);
            if (string.IsNullOrEmpty(songFilename)) {
                logger.LogWarning("Map audio: no song filename in Info.dat for map {MapId}", beatmapId);
                return null;
            }

            var audio = source.TryReadFile(songFilename);
            if (audio is null) {
                logger.LogWarning("Map audio: '{File}' not present in zip for map {MapId}", songFilename, beatmapId);
                return null;
            }

            return new MapAudio(audio, ContentTypeFor(songFilename), songFilename);
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            logger.LogError(ex, "Map audio: failed to extract song for map {MapId}", beatmapId);
            return null;
        }
    }

    /// <summary>Maps a Beat Saber audio filename to its MIME type. <c>.egg</c> is Ogg Vorbis.</summary>
    private static string ContentTypeFor(string filename) =>
        Path.GetExtension(filename).ToLowerInvariant() switch {
            ".egg" or ".ogg" => "audio/ogg",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            _ => "application/octet-stream",
        };
}
