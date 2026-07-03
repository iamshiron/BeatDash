using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shiron.BeatDash.API.Configuration;
using Shiron.BeatDash.API.Services.Socket;
using Shiron.BeatDash.Data.Socket;

namespace Shiron.BeatDash.API.Services;

/// <summary>
/// Receives UDP datagrams and routes binary game packets through the same
/// <see cref="SocketBinaryDispatcher"/> used by the TCP WebSocket, so handlers are
/// shared across both transports. Until an endpoint is bound via a
/// <see cref="BinaryPacketTypes.Holepunch"/> handshake, only that packet type is
/// accepted; everything else is dropped. On a successful bind the server notifies
/// the client over TCP (see <see cref="UdpBoundMessage"/>).
/// </summary>
public class UdpSocketService(
    ILogger<UdpSocketService> logger,
    IOptions<UdpSocketOptions> udpOptions,
    ISessionManager sessionManager,
    IServiceScopeFactory scopeFactory
) : BackgroundService {
    private readonly UdpSocketOptions _udp = udpOptions.Value;

    protected async override Task ExecuteAsync(CancellationToken stoppingToken) {
        if (_udp.Port is <= 0 or > ushort.MaxValue) throw new InvalidOperationException($"Invalid port number: {_udp.Port}");

        var bindAddress = IPAddress.Parse(_udp.Host);
        using var client = new UdpClient(new IPEndPoint(bindAddress, _udp.Port));

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Binding UDP socket to {Host}:{Port}", _udp.Host, _udp.Port);

        while (!stoppingToken.IsCancellationRequested) {
            UdpReceiveResult result;
            try {
                result = await client.ReceiveAsync(stoppingToken);
            } catch (OperationCanceledException) {
                break;
            } catch (SocketException ex) {
                if (logger.IsEnabled(LogLevel.Warning))
                    logger.LogWarning("Transient socket error on UDP receive (continuing): {Message}", ex.Message);
                continue;
            }

            _ = ProcessPacketAsync(result.Buffer, result.RemoteEndPoint);
        }

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("UDP socket service stopped");
    }

    /// <summary>
    /// Strips the <see cref="UdpPacket"/> envelope and routes the packet: the
    /// Holepunch handshake is handled inline (pre-auth), all other types go through
    /// the central binary dispatcher shared with the TCP path (post-auth).
    /// </summary>
    private async ValueTask ProcessPacketAsync(byte[] data, IPEndPoint endpoint) {
        try {
            if (data.Length < UdpPacket.HeaderSize) {
                LogDropped("malformed", data.Length, endpoint);
                return;
            }

            var typeByte = data[UdpPacket.HeaderSize - 1];
            if (!Enum.IsDefined(typeof(BinaryPacketTypes), typeByte)) {
                LogDropped("unknown type", data.Length, endpoint);
                return;
            }

            var type = (BinaryPacketTypes) typeByte;

            // The Holepunch handshake is the only packet accepted before authentication.
            if (type == BinaryPacketTypes.Holepunch) {
                var ticketBytes = data.AsSpan(UdpPacket.HeaderSize);
                if (ticketBytes.Length != 16) {
                    logger.LogWarning("Dropped holepunch with invalid payload ({Bytes} bytes) from {Endpoint}", ticketBytes.Length, endpoint);
                    return;
                }
                await AuthenticateAndAckAsync(new Guid(ticketBytes), endpoint);
                return;
            }

            // All other packets require an already-bound endpoint.
            var session = sessionManager.GetSessionByUdpEndPoint(endpoint);
            if (session is null) {
                if (logger.IsEnabled(LogLevel.Debug))
                    logger.LogDebug("Dropped {Type} packet from unauthenticated {Endpoint}", type, endpoint);
                return;
            }

            await DispatchAsync(session, type, data.AsMemory(UdpPacket.HeaderSize));
        } catch (Exception ex) {
            logger.LogError(ex, "Error processing packet from {Endpoint}", endpoint);
        }
    }

    /// <summary>
    /// Binds the endpoint via the ticket, then notifies the client over TCP that UDP is ready.
    /// </summary>
    private async Task AuthenticateAndAckAsync(Guid ticket, IPEndPoint endpoint) {
        var session = sessionManager.AuthenticateUdp(ticket, endpoint);
        if (session is null) {
            logger.LogWarning("Failed UDP authentication from {Endpoint}: invalid or expired ticket", endpoint);
            return;
        }

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Bound UDP endpoint {Endpoint} to Session {SessionId}", endpoint, session.Id);

        await sessionManager.SendMessageAsync(session.Id, new UdpBoundMessage(), CancellationToken.None);
    }

    /// <summary>
    /// Routes an authenticated datagram through the central binary dispatcher,
    /// reusing the same handlers as the TCP WebSocket path.
    /// </summary>
    private async ValueTask DispatchAsync(Session session, BinaryPacketTypes type, ReadOnlyMemory<byte> content) {
        using var scope = scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<SocketBinaryDispatcher>();
        var context = new SocketContext(session.UserId, session.ClientId, session.Id, sessionManager);
        await dispatcher.DispatchAsync(context, type, content, CancellationToken.None);
    }

    private void LogDropped(string reason, int bytes, IPEndPoint endpoint) {
        if (logger.IsEnabled(LogLevel.Warning))
            logger.LogWarning("Dropped {Reason} UDP packet ({Bytes} bytes) from {Endpoint}", reason, bytes, endpoint);
    }
}
