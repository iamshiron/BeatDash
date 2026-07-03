using System.Net;
using System.Net.Sockets;

namespace Shiron.BeatDash.API.Services;

public class UdpSocketService(ILogger<UdpSocketService> logger, IConfiguration config) : BackgroundService {
    private readonly int _port = config.GetSection("UdpSocket").GetValue<int>("Port");
    private readonly string _host = config.GetSection("UdpSocket").GetValue<string>("Host") ?? "localhost";

    protected async override Task ExecuteAsync(CancellationToken stoppingToken) {
        if (_port <= 0 || _port > ushort.MaxValue) throw new InvalidOperationException($"Invalid port number: {_port}");

        var bindAddress = IPAddress.Parse(_host);
        using var client = new UdpClient(new IPEndPoint(bindAddress, _port));
        logger.LogInformation($"Binding UDP socket to {_host}:{_port}");

        try {
            while (!stoppingToken.IsCancellationRequested) {
                var result = await client.ReceiveAsync(stoppingToken);
                var endpoint = result.RemoteEndPoint;

                var data = result.Buffer;
                logger.LogInformation($"Received {data.Length} bytes from {endpoint}");
                await ProcessBinaryData(data, endpoint);
            }
        } catch (OperationCanceledException e) {
            logger.LogInformation($"UDP socket service stopped: {e.Message}");
        } catch (Exception e) {
            logger.LogError(e, "Error in UDP socket service");
        }
    }

    private ValueTask ProcessBinaryData(byte[] data, IPEndPoint endpoint) {
        return ValueTask.CompletedTask;
    }
}
