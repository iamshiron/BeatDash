using System.ComponentModel.DataAnnotations;

namespace Shiron.BeatDash.DB.Schema.BeatSaver;

/// <summary>
/// A BeatSaver uploader/mapper account, deduplicated by its BeatSaver user id
/// so a mapper's many uploads share a single row.
/// </summary>
public sealed class BeatSaverUser {
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>The BeatSaver-assigned numeric user id. Unique.</summary>
    public required long BeatSaverUserId { get; set; }

    [MaxLength(256)] public required string Name { get; set; }
    [MaxLength(128)] public string? Hash { get; set; }
    [MaxLength(1024)] public string? Avatar { get; set; }

    /// <summary>Account type reported by BeatSaver (e.g. <c>DISCORD</c>).</summary>
    [MaxLength(32)] public string? Type { get; set; }

    public required bool Admin { get; set; }
    public required bool Curator { get; set; }
    public required bool SeniorCurator { get; set; }

    [MaxLength(1024)] public string? PlaylistUrl { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
