using IPA;
using IPA.Loader;
using IpaLogger = IPA.Logging.Logger;

namespace Shiron.BeatDash.Mod;

[Plugin(RuntimeOptions.DynamicInit)]
internal class Plugin {
    internal static IpaLogger Log { get; private set; } = null!;

    [Init]
    public Plugin(IpaLogger ipaLogger, PluginMetadata pluginMetadata) {
        Log = ipaLogger;
        Log.Info($"{pluginMetadata.Name} {pluginMetadata.HVersion} initialized.");
    }

    [OnStart]
    public void OnApplicationStart() {
        Log.Debug("OnApplicationStart");
    }

    [OnExit]
    public void OnApplicationQuit() {
        Log.Debug("OnApplicationQuit");
    }
}
