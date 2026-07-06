using System.ComponentModel.DataAnnotations;
using Shiron.BeatDash.DB.Schema.BeatSaver;

namespace Shiron.BeatDash.DB.Schema;

/// <summary>
/// A beatmap (song/level), deduplicated by its game <see cref="LevelId"/>.
/// Holds song-level metadata and a link to the cover image stored in MinIO.
/// </summary>
public sealed class Beatmap {
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// The game-assigned level identifier (e.g. <c>custom_level_...</c>).
    /// Unique across all maps.
    /// </summary>
    [MaxLength(255)] public required string LevelId { get; set; }

    [MaxLength(256)] public required string SongName { get; set; }
    [MaxLength(256)] public string? SongSubName { get; set; }
    [MaxLength(256)] public required string SongAuthor { get; set; }
    [MaxLength(256)] public required string Mapper { get; set; }

    public required float Bpm { get; set; }
    public required int DurationMs { get; set; }

    /// <summary>
    /// The MinIO object key for the cover image (e.g. <c>covers/{guid}.png</c>),
    /// resolved against the assets bucket. <see langword="null"/> until uploaded.
    /// </summary>
    [MaxLength(512)] public string? CoverImageKey { get; set; }

    /// <summary>
    /// The user who first submitted this map. Kept for audit purposes only;
    /// nulled (not cascade-deleted) if the user is removed.
    /// </summary>
    public Guid? SubmittedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // --- BeatSaver fetch bookkeeping ---

    /// <summary>
    /// Lifecycle of the BeatSaver metadata fetch for this map. Drives which maps
    /// the fetch job picks up. Defaults to <see cref="BeatSaverFetchStatus.Pending"/>.
    /// </summary>
    public BeatSaverFetchStatus FetchStatus { get; set; } = BeatSaverFetchStatus.Pending;

    /// <summary>When a fetch was last attempted (success or failure), if ever.</summary>
    public DateTime? FetchLastAttemptedAt { get; set; }

    /// <summary>Number of fetch attempts made; used to cap retries of failures.</summary>
    public int FetchAttemptCount { get; set; }

    /// <summary>The last fetch error message, if the most recent attempt failed.</summary>
    [MaxLength(1024)] public string? FetchError { get; set; }

    public ICollection<BeatmapDifficulty> Difficulties { get; set; } = [];

    /// <summary>The fetched BeatSaver record, if any. <see langword="null"/> until fetched.</summary>
    public BeatSaverMap? BeatSaverMap { get; set; }
}
