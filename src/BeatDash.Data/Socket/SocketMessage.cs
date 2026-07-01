namespace Shiron.BeatDash.Data.Socket;

public abstract class SocketMessage<T> {
    public string Type { get; } = typeof(T).Name;
}
