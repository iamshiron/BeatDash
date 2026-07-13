namespace Shiron.BeatDash.DB.Schema;

/// <summary>
/// Membership of a single <see cref="Beatmap"/> in a <see cref="MapList"/>. At most one
/// row per <c>(MapListId, BeatmapId)</c>, enforced by a unique composite index. Cascade-
/// deletes with either the owning list or the referenced map.
/// </summary>
public sealed class MapListItem {
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid MapListId { get; set; }
    public MapList MapList { get; set; } = null!;

    public Guid BeatmapId { get; set; }
    public Beatmap Beatmap { get; set; } = null!;

    /// <summary>Sort position within the list; lower comes first. New maps append at the end.</summary>
    public int Position { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
