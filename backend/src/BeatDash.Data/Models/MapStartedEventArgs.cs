namespace Shiron.BeatDash.Data.Models;

public class MapStartedEventArgs : EventArgs {
    public required MapSessionData Session { get; init; }
}
