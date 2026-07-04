namespace Shiron.BeatDash.DB.Schema;

public abstract class PlaySessionItem {
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required int CorrelationId { get; set; }

    public required Guid PlaySessionId { get; set; }
    public PlaySession PlaySession { get; set; } = null!;

    public required int SongTimeMs { get; set; }
}
