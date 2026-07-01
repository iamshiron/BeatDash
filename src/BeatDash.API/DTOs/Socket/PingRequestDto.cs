namespace Shiron.BeatDash.API.DTOs.Socket;

public class PingRequestDto : SocketMessage<PingRequestDto> {
    public required string Message { get; init; }
}
