using Microsoft.Extensions.DependencyInjection;
using Shiron.BeatDash.Data.Socket;

namespace Shiron.BeatDash.API.Services.Socket;

public static class SocketServiceCollectionExtensions {
    /// <summary>
    /// Registers a scoped handler for a specific socket text message type.
    /// The handler is keyed by the message type name (e.g. "MapStartMessage")
    /// and resolved by <see cref="SocketMessageDispatcher"/> when a matching message arrives.
    /// </summary>
    /// <typeparam name="TMessage">The socket message DTO to handle.</typeparam>
    /// <typeparam name="THandler">The handler implementation.</typeparam>
    /// <example>
    /// <code>
    /// builder.Services.AddSocketMessageHandler&lt;MapStartMessage, MapStartHandler&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddSocketMessageHandler<TMessage, THandler>(this IServiceCollection services)
        where TMessage : SocketMessage<TMessage>
        where THandler : class, ISocketMessageHandler {
        services.AddKeyedScoped<ISocketMessageHandler, THandler>(typeof(TMessage).Name);
        return services;
    }

    /// <summary>
    /// Registers a scoped handler for a specific binary packet type.
    /// The handler is keyed by the <see cref="BinaryPacketTypes"/> enum value
    /// and resolved by <see cref="SocketBinaryDispatcher"/> when a matching packet arrives.
    /// </summary>
    /// <typeparam name="THandler">The handler implementation.</typeparam>
    /// <param name="packetType">The binary packet type to handle.</param>
    /// <example>
    /// <code>
    /// builder.Services.AddSocketBinaryHandler&lt;MapCoverImageHandler&gt;(BinaryPacketTypes.MapCoverImage);
    /// </code>
    /// </example>
    public static IServiceCollection AddSocketBinaryHandler<THandler>(this IServiceCollection services, BinaryPacketTypes packetType)
        where THandler : class, ISocketBinaryHandler {
        services.AddKeyedScoped<ISocketBinaryHandler, THandler>(packetType);
        return services;
    }
}
