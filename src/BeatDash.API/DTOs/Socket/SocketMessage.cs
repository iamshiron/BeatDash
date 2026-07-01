namespace Shiron.BeatDash.API.DTOs.Socket;

public abstract class SocketMessage<T> {
    public string Type { get; } = typeof(T).Name;
}
