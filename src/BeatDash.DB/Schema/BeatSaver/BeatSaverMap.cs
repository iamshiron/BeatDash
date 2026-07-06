using System.ComponentModel.DataAnnotations;

namespace Shiron.BeatDash.DB.Schema.BeatSaver;

/// <summary>
/// The BeatSaver record for a <see cref="Beatmap"/>, fetched from the BeatSaver
/// API and downloaded to object storage. One row per beatmap (1:1), created only
/// once a fetch succeeds. Rich, normalized child data lives in
/// <see cref="Versions"/>, <see cref="Tags"/> and the shared <see cref="Uploader"/>.
/// </summary>
public sealed class BeatSaverMap {
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>The BeatSaver key/id (e.g. <c>"84f2"</c>).</summary>
    [MaxLength(32)] public required string BeatSaverId { get; set; }

    [MaxLength(512)] public required string Name { get; set; }

    /// <summary>Free-form description (unbounded text). May be empty.</summary>
    public string? Description { get; set; }

    public required bool Automapper { get; set; }
    public required bool Ranked { get; set; }
    public required bool Qualified { get; set; }
    public required bool BlRanked { get; set; }
    public required bool BlQualified { get; set; }

    /// <summary>AI-generation declaration (e.g. <c>"None"</c>).</summary>
    [MaxLength(32)] public string? DeclaredAi { get; set; }

    public DateTime? Uploaded { get; set; }
    public DateTime? BeatSaverCreatedAt { get; set; }
    public DateTime? BeatSaverUpdatedAt { get; set; }
    public DateTime? LastPublishedAt { get; set; }

    /// <summary>
    /// The MinIO object key for the downloaded map zip (<c>maps/{beatmap-guid}.zip</c>),
    /// resolved against the assets bucket. <see langword="null"/> until downloaded.
    /// </summary>
    [MaxLength(512)] public string? ZipObjectKey { get; set; }

    /// <summary>When this BeatSaver record was last successfully fetched.</summary>
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

    public required BeatSaverMetadata Metadata { get; set; }
    public required BeatSaverStats Stats { get; set; }

    /// <summary>The uploader, deduplicated across maps. Nulled if the user row is removed.</summary>
    public Guid? UploaderId { get; set; }
    public BeatSaverUser? Uploader { get; set; }

    public Guid BeatmapId { get; set; }
    public Beatmap Beatmap { get; set; } = null!;

    /// <summary>Free-form tags (e.g. <c>rock</c>, <c>nightcore</c>), stored as a Postgres text array.</summary>
    public List<string> Tags { get; set; } = [];

    public ICollection<BeatSaverVersion> Versions { get; set; } = [];
}
