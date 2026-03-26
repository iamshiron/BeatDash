using System.Net.WebSockets;

namespace Shiron.BeatDash.API.Services;

public class WebSocketConnection {
    public string Name { get; }
    public Uri Uri { get; }
    public ClientWebSocket Client { get; }
    public CancellationTokenSource CancellationTokenSource { get; }

    public WebSocketConnection(string name, string uri) {
        Name = name;
        Uri = new Uri(uri);
        Client = new ClientWebSocket();
        CancellationTokenSource = new CancellationTokenSource();
    }
}
