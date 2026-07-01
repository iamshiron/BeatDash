using System;
using Shiron.BeatDash.Mod.Network;
using Zenject;

namespace Shiron.BeatDash.Mod;

public class PluginStartup(NetworkManager networkManager, PluginConfig config) : IInitializable, IDisposable {
    public async void Initialize() {
        Plugin.Log.Info("Initializing BeatDash");

        if (string.IsNullOrEmpty(config.AccessToken)) {
            Plugin.Log.Warn("No access token found. Please authenticate in the BeatDash settings menu.");
            return;
        }

        Plugin.Log.Info("Connecting to BeatDash socket");
        try {
            await networkManager.ConnectToSocketAsync();
        } catch (Exception e) {
            Plugin.Log.Error($"Failed to connect to BeatDash socket: {e.Message}");
        }
    }
    public void Dispose() {
        Plugin.Log.Info("Disposing BeatDash");
        networkManager.Dispose();
    }
}
