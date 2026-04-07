namespace Shiron.BeatDash.Data.Models;

public class EndpointStats {
    public string Name { get; }
    public ConnectionStatus Status { get; set; } = ConnectionStatus.Disconnected;
    public int MessageCount { get; set; }
    public DateTimeOffset? LastMessageTime { get; set; }

    public EndpointStats(string name) {
        Name = name;
    }
}
