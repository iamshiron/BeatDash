using System.ComponentModel.DataAnnotations;

namespace Shiron.BeatDash.DB.Schema;

/// <summary>
/// A user-created, named collection of maps (e.g. "Warmup", "Trying to Beat").
/// A user may own any number of lists, and a map may appear in any number of lists.
/// Owned by a <see cref="User"/>; cascade-deleted with the user.
/// </summary>
public sealed class MapList {
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    [MaxLength(64)] public required string Name { get; set; }
    [MaxLength(512)] public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<MapListItem> Items { get; set; } = [];
}
