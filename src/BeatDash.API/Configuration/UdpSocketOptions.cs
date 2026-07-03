namespace Shiron.BeatDash.API.Configuration;

/// <summary>
/// UDP socket bind settings, bound from the "UdpSocket" configuration section.
/// </summary>
public sealed class UdpSocketOptions {
    public int Port { get; set; }
    public string Host { get; set; } = "localhost";
}
