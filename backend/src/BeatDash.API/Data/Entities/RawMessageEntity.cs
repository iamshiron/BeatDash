namespace Shiron.BeatDash.API.Data.Entities;

public class RawMessageEntity {
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string ConnectionName { get; set; } = "";
    public string Message { get; set; } = "";
    public long? PlaySessionId { get; set; }
    public PlaySessionEntity? PlaySession { get; set; }
}
