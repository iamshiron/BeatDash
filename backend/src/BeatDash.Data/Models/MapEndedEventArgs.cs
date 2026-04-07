namespace Shiron.BeatDash.Data.Models;

public class MapEndedEventArgs : EventArgs {
    public required MapSessionData Session { get; init; }
}
