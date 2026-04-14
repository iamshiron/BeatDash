namespace Shiron.BeatDash.Data.Models;

public record RecordedMessage(DateTimeOffset Timestamp, string Endpoint, string Message);
