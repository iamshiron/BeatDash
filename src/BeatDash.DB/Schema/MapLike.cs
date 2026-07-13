namespace Shiron.BeatDash.DB.Schema;

/// <summary>
/// A user's like of a whole <see cref="Beatmap"/> (the map/song, not a specific
/// difficulty). At most one row per <c>(UserId, BeatmapId)</c>, enforced by a unique
/// composite index. Both sides cascade-delete: removing the user or the map removes
/// the like.
/// </summary>
public sealed class MapLike {
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid BeatmapId { get; set; }
    public Beatmap Beatmap { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
