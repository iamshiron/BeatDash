using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;
using Shiron.BeatDash.API.Configuration;

namespace Shiron.BeatDash.API.Endpoints;

public static class ServerInfoEndpoints {
    public static void MapServerInfoEndpoints(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup("/server").WithTags("Server");

        group.MapGet("/", (IServer server, IOptions<UdpSocketOptions> udpOptions, IOptions<HspOptions> hspOptions) => {
            var listener = GetBoundAddress(server);
            var apiPort = listener?.Port ?? 0;
            var hostAddress = GetLanIpAddress();
            var ingestUrl = GetHspIngestUrl(hspOptions.Value, listener, hostAddress);

            return Results.Ok(new ServerInfoDto(
                udpOptions.Value.Port,
                apiPort,
                hostAddress,
                ingestUrl
            ));
        }).Produces<ServerInfoDto>().RequireAuthorization();
    }

    private static Uri? GetBoundAddress(IServer server) {
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
        if (addresses is null) {
            return null;
        }

        foreach (var address in addresses) {
            if (Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Port > 0) {
                return uri;
            }
        }

        return null;
    }

    internal static string GetHspIngestUrl(HspOptions options, Uri? listener, string hostAddress) {
        var publicUri = options.GetPublicIngestUri();
        if (publicUri is not null) return publicUri.AbsoluteUri;

        var builder = new UriBuilder(listener ?? new Uri("http://localhost")) {
            Host = hostAddress,
            Path = "/api/hsp/ingest",
            Query = string.Empty,
            Fragment = string.Empty
        };
        return builder.Uri.AbsoluteUri;
    }

    private static string GetLanIpAddress() {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces()) {
            if (ni.OperationalStatus != OperationalStatus.Up) {
                continue;
            }
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) {
                continue;
            }

            foreach (var addr in ni.GetIPProperties().UnicastAddresses) {
                if (addr.Address.AddressFamily == AddressFamily.InterNetwork) {
                    return addr.Address.ToString();
                }
            }
        }

        return IPAddress.Loopback.ToString();
    }
}

/// <summary>
/// Information about the running BeatDash server, so clients on the local
/// network know where to connect.
/// </summary>
/// <param name="UdpPort">The UDP port the socket service is bound to.</param>
/// <param name="ApiPort">The TCP port the HTTP API is listening on.</param>
/// <param name="HostAddress">The LAN IPv4 address other machines can reach the server at.</param>
/// <param name="HspIngestUrl">The full URL HSP clients post sensor samples to.</param>
public sealed record ServerInfoDto(int UdpPort, int ApiPort, string HostAddress, string HspIngestUrl);
