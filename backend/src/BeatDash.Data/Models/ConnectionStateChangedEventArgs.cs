namespace Shiron.BeatDash.Data.Models;

public class ConnectionStateChangedEventArgs : EventArgs {
    public required string EndpointName { get; init; }
    public required ConnectionStatus Status { get; init; }
}
