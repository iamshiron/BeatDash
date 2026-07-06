using System.ComponentModel.DataAnnotations;

namespace Shiron.BeatDash.DB.Schema.BeatSaver;

/// <summary>
/// A published version of a <see cref="BeatSaverMap"/>, identified by its file
/// hash and carrying the download/cover/preview URLs plus per-difficulty stats.
/// </summary>
public sealed class BeatSaverVersion {
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [MaxLength(128)] public required string Hash { get; set; }
    [MaxLength(32)] public string? Key { get; set; }
    [MaxLength(32)] public string? State { get; set; }

    public DateTime? CreatedAt { get; set; }
    public int? SageScore { get; set; }

    [MaxLength(1024)] public string? DownloadUrl { get; set; }
    [MaxLength(1024)] public string? CoverUrl { get; set; }
    [MaxLength(1024)] public string? PreviewUrl { get; set; }

    public Guid BeatSaverMapId { get; set; }
    public BeatSaverMap BeatSaverMap { get; set; } = null!;

    public ICollection<BeatSaverVersionDifficulty> Difficulties { get; set; } = [];
}
